using Common.Models;
using Common.Services;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using Polly;
using Polly.Retry;

namespace OrderProcessingService.Services;

public class MqttSubscriberService : IMqttSubscriberService
{
    private readonly ILogger<MqttSubscriberService> _logger;
    private readonly MqttSettings _settings;
    private readonly IMetricsService _metrics;
    private IMqttClient? _client;
    private readonly MqttFactory _factory;
    private readonly AsyncRetryPolicy _retryPolicy;
    private bool _isConnected;
    private MqttClientOptions? _options;
    private readonly SemaphoreSlim _connectionLock = new SemaphoreSlim(1, 1);
    private bool _isSubscribed;
    private readonly HashSet<string> _processedMessageIds = new();
    private readonly SemaphoreSlim _messageLock = new SemaphoreSlim(1, 1);
    private Action<string>? _messageHandler;
    private string? _currentTopic;

    public bool IsConnected => _isConnected;

    public event Func<string, string, Task>? MessageReceived;

    public MqttSubscriberService(
        ILogger<MqttSubscriberService> logger, 
        MqttSettings settings,
        IMetricsService metrics)
    {
        _logger = logger;
        _settings = settings;
        _metrics = metrics;
        _factory = new MqttFactory();
        
        _retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                _settings.RetryPolicy.MaxRetries,
                retryAttempt => 
                    TimeSpan.FromSeconds(_settings.RetryPolicy.DelaySeconds * Math.Pow(2, retryAttempt - 1)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(exception, 
                        "Yritys {RetryCount}/{MaxRetries} epäonnistui. Odotetaan {DelaySeconds} sekuntia.",
                        retryCount, _settings.RetryPolicy.MaxRetries, timeSpan.TotalSeconds);
                });
    }

    private async Task HandleDisconnectAsync(MqttClientDisconnectedEventArgs e)
    {
        _isConnected = false;
        _isSubscribed = false;
        _logger.LogWarning("MQTT-yhteys katkesi: {Reason}", e.Reason);

        try
        {
            await _connectionLock.WaitAsync();
            try 
            {
                // Odotetaan pidempi aika ennen uudelleenyhdistämistä
                await Task.Delay(TimeSpan.FromSeconds(5));

                if (_client != null && _options != null && !_client.IsConnected)
                {
                    await _client.ConnectAsync(_options);
                    
                    // Odotetaan yhteyden muodostumista
                    var timeout = TimeSpan.FromSeconds(10);
                    var start = DateTime.UtcNow;
                    while (!_isConnected && DateTime.UtcNow - start < timeout)
                    {
                        await Task.Delay(100);
                    }

                    if (!_isConnected)
                    {
                        throw new TimeoutException("MQTT-yhteyden muodostaminen aikakatkaistiin");
                    }

                    _logger.LogInformation("Yhteys muodostettu uudelleen");

                    // Tilataan aihe uudelleen jos se on asetettu
                    if (!string.IsNullOrEmpty(_currentTopic))
                    {
                        await SubscribeToTopicAsync(_currentTopic);
                    }
                }
            }
            finally
            {
                _connectionLock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Virhe yhteyden uudelleenmuodostamisessa");
        }
    }

    private async Task HandleMessageAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        await _messageLock.WaitAsync();
        try
        {
            var messageId = Convert.ToBase64String(e.ApplicationMessage.PayloadSegment.ToArray());
            if (_processedMessageIds.Contains(messageId))
            {
                _logger.LogInformation("Viesti on jo käsitelty: {MessageId}", messageId);
                return;
            }

            var message = System.Text.Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);
            var topic = e.ApplicationMessage.Topic;

            var startTime = DateTime.UtcNow;

            // Kutsutaan vanhaa messageHandler-delegaattia
            _messageHandler?.Invoke(message);

            // Kutsutaan uutta MessageReceived-tapahtumaa
            if (MessageReceived != null)
            {
                await MessageReceived.Invoke(topic, message);
            }

            var duration = DateTime.UtcNow - startTime;
            _metrics.RecordProcessingTime(topic, duration);
            _metrics.IncrementProcessedOrders();
            _processedMessageIds.Add(messageId);

            // Siivotaan vanhat viestit (pidetään vain viimeiset 1000)
            if (_processedMessageIds.Count > 1000)
            {
                _processedMessageIds.Clear();
            }
        }
        finally
        {
            _messageLock.Release();
        }
    }

    public async Task ConnectAsync()
    {
        await _retryPolicy.ExecuteAsync(async () =>
        {
            try
            {
                _client = _factory.CreateMqttClient();

                var optionsBuilder = new MqttClientOptionsBuilder()
                    .WithTcpServer(_settings.BrokerAddress, _settings.BrokerPort)
                    .WithClientId($"{_settings.ClientId}-{Guid.NewGuid()}")
                    .WithCredentials(_settings.Username, _settings.Password)
                    .WithKeepAlivePeriod(TimeSpan.FromSeconds(120))
                    .WithCleanSession(false);

                if (_settings.UseTls)
                {
                    optionsBuilder.WithTls(new MqttClientOptionsBuilderTlsParameters
                    {
                        UseTls = true,
                        AllowUntrustedCertificates = true,
                        IgnoreCertificateChainErrors = true,
                        IgnoreCertificateRevocationErrors = true
                    });
                }

                _options = optionsBuilder.Build();

                _client.DisconnectedAsync += async e => await HandleDisconnectAsync(e);

                _client.ConnectedAsync += async e =>
                {
                    _isConnected = true;
                    _logger.LogInformation("MQTT-yhteys muodostettu");
                    await Task.CompletedTask;
                };

                _client.ApplicationMessageReceivedAsync += HandleMessageAsync;

                await _client.ConnectAsync(_options);
                
                // Odotetaan yhteyden muodostumista
                var timeout = TimeSpan.FromSeconds(10);
                var start = DateTime.UtcNow;
                while (!_isConnected && DateTime.UtcNow - start < timeout)
                {
                    await Task.Delay(100);
                }

                if (!_isConnected)
                {
                    throw new TimeoutException("MQTT-yhteyden muodostaminen aikakatkaistiin");
                }

                _logger.LogInformation("Yhdistetty MQTT-brokeriin: {BrokerAddress}:{BrokerPort}", 
                    _settings.BrokerAddress, _settings.BrokerPort);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Virhe MQTT-yhteyden muodostamisessa");
                throw;
            }
        });
    }

    private async Task SubscribeToTopicAsync(string topic)
    {
        if (_isSubscribed)
        {
            _logger.LogInformation("Aihe {Topic} on jo tilattu", topic);
            return;
        }

        var subscribeOptions = _factory.CreateSubscribeOptionsBuilder()
            .WithTopicFilter(topic, MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        await _client!.SubscribeAsync(subscribeOptions);
        _isSubscribed = true;
        _currentTopic = topic;
        _logger.LogInformation("Tilattu aihe: {Topic}", topic);
    }

    public async Task SubscribeAsync(string topic)
    {
        await _retryPolicy.ExecuteAsync(async () =>
        {
            await _connectionLock.WaitAsync();
            try
            {
                if (_client == null || !_isConnected)
                {
                    await ConnectAsync();
                }

                if (_client == null)
                {
                    throw new InvalidOperationException("MQTT-asiakasta ei voitu luoda");
                }

                await SubscribeToTopicAsync(topic);
            }
            finally
            {
                _connectionLock.Release();
            }
        });
    }

    public async Task SubscribeAsync(string topic, Action<string> messageHandler)
    {
        _messageHandler = messageHandler;
        await SubscribeAsync(topic);
    }

    public async Task UnsubscribeAsync(string topic)
    {
        if (_client == null || !_isConnected)
        {
            return;
        }

        await _client.UnsubscribeAsync(topic);
        _isSubscribed = false;
        _currentTopic = null;
        _messageHandler = null;
        _logger.LogInformation("Tilaus peruttu aiheesta: {Topic}", topic);
    }

    public async Task DisconnectAsync()
    {
        if (_client != null && _client.IsConnected)
        {
            await _client.DisconnectAsync();
            _isConnected = false;
            _isSubscribed = false;
            _currentTopic = null;
            _messageHandler = null;
            _logger.LogInformation("Yhteys MQTT-brokeriin katkaistu");
        }
    }
} 