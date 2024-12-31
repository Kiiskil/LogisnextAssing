using Common.Models;
using Common.Services;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using Polly;
using Polly.Retry;
using System.Threading;

namespace OrderProcessingService.Services;

public class MqttPublisherService : IMqttPublisherService
{
    private readonly ILogger<MqttPublisherService> _logger;
    private readonly MqttSettings _settings;
    private readonly IMetricsService _metrics;
    private IMqttClient? _client;
    private readonly MqttFactory _factory;
    private readonly AsyncRetryPolicy _retryPolicy;
    private bool _isConnected;
    private MqttClientOptions? _options;
    private readonly SemaphoreSlim _connectionLock = new SemaphoreSlim(1, 1);

    public MqttPublisherService(
        ILogger<MqttPublisherService> logger, 
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
        _logger.LogWarning("MQTT-yhteys katkesi: {Reason}", e.Reason);

        try
        {
            await _connectionLock.WaitAsync();
            try
            {
                // Odotetaan hetki ennen uudelleenyhdistämistä
                await Task.Delay(TimeSpan.FromSeconds(5));

                if (_client != null && _options != null && !_client.IsConnected)
                {
                    await _client.ConnectAsync(_options);
                    _logger.LogInformation("Yhteys muodostettu uudelleen");
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

    public async Task ConnectAsync()
    {
        await _retryPolicy.ExecuteAsync(async () =>
        {
            await _connectionLock.WaitAsync();
            try
            {
                _client = _factory.CreateMqttClient();

                var optionsBuilder = new MqttClientOptionsBuilder()
                    .WithTcpServer(_settings.BrokerAddress, _settings.BrokerPort)
                    .WithClientId(_settings.ClientId)
                    .WithCredentials(_settings.Username, _settings.Password)
                    .WithKeepAlivePeriod(TimeSpan.FromSeconds(60))
                    .WithCleanSession();

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
            finally
            {
                _connectionLock.Release();
            }
        });
    }

    public async Task PublishAsync(string topic, string message)
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
                    var messageBuilder = new MqttApplicationMessageBuilder()
                        .WithTopic(topic)
                        .WithPayload(message)
                        .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                        .WithRetainFlag(false)
                        .Build();

                    await _client.PublishAsync(messageBuilder);
                    _metrics.IncrementProcessedOrders();
                    _logger.LogInformation("Viesti julkaistu aiheeseen {Topic}", topic);
                }
                catch (Exception ex)
                {
                    _metrics.IncrementFailedOrders();
                    _logger.LogError(ex, "Virhe viestin julkaisussa aiheeseen {Topic}", topic);
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