using Common.Models;
using Common.Services;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using Polly;
using Polly.Retry;
using System.Threading;

namespace OrderProcessingService.Services;

public class MqttSubscriberService : IMqttSubscriberService
{
    private readonly ILogger<MqttSubscriberService> _logger;
    private readonly MqttSettings _settings;
    private readonly IMetricsService _metrics;
    private IMqttClient? _client;
    private readonly MqttFactory _factory;
    private readonly AsyncRetryPolicy _retryPolicy;
    private Action<string>? _messageHandler;
    private bool _isConnected;
    private MqttClientOptions? _options;
    private string? _currentTopic;
    private readonly SemaphoreSlim _connectionLock = new SemaphoreSlim(1, 1);
    private bool _isSubscribed;
    private readonly HashSet<string> _processedMessageIds = new();
    private readonly SemaphoreSlim _messageLock = new SemaphoreSlim(1, 1);

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
                    if (!string.IsNullOrEmpty(_currentTopic) && _messageHandler != null)
                    {
                        await SubscribeToTopicAsync(_currentTopic, _messageHandler);
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

    private async Task SubscribeToTopicAsync(string topic, Action<string> messageHandler)
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
        _logger.LogInformation("Tilattu aihe: {Topic}", topic);
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

            if (_messageHandler != null)
            {
                var startTime = DateTime.UtcNow;
                var message = System.Text.Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);
                _messageHandler(message);
                var duration = DateTime.UtcNow - startTime;
                _metrics.RecordProcessingTime(e.ApplicationMessage.Topic, duration);
                _metrics.IncrementProcessedOrders();
                _processedMessageIds.Add(messageId);

                // Siivotaan vanhat viestit (pidetään vain viimeiset 1000)
                if (_processedMessageIds.Count > 1000)
                {
                    _processedMessageIds.Clear();
                }
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
                    optionsBuilder.WithTlsOptions(o => { });
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

    public async Task SubscribeAsync(string topic, Action<string> messageHandler)
    {
        await _retryPolicy.ExecuteAsync(async () =>
        {
            await _connectionLock.WaitAsync();
            try
            {
                if (_client == null || !_isConnected)
                {
                    // Jos yhteys on katkennut, yritetään muodostaa se uudelleen
                    await ConnectAsync();
                }

                if (_client == null)
                {
                    throw new InvalidOperationException("MQTT-asiakasta ei voitu luoda");
                }

                try
                {
                    _messageHandler = messageHandler;
                    _currentTopic = topic;
                    await SubscribeToTopicAsync(topic, messageHandler);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Virhe aiheen tilaamisessa: {Topic}", topic);
                    throw;
                }
            }
            finally
            {
                _connectionLock.Release();
            }
        });
    }

    public async Task DisconnectAsync()
    {
        if (_client != null && _isConnected)
        {
            try
            {
                await _client.DisconnectAsync();
                _isConnected = false;
                _isSubscribed = false;
                _logger.LogInformation("Yhteys MQTT-brokeriin katkaistu");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Virhe MQTT-yhteyden katkaisussa");
                throw;
            }
        }
    }
} 