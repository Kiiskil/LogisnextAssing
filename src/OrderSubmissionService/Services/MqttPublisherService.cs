using Common.Models;
using Common.Services;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using Polly;
using Polly.Retry;

namespace OrderSubmissionService.Services;

public class MqttPublisherService : IMqttPublisherService
{
    private readonly ILogger<MqttPublisherService> _logger;
    private readonly MqttSettings _settings;
    private readonly IMetricsService _metrics;
    private IMqttClient? _client;
    private readonly MqttFactory _factory;
    private readonly AsyncRetryPolicy _retryPolicy;

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

    public async Task ConnectAsync()
    {
        await _retryPolicy.ExecuteAsync(async () =>
        {
            try
            {
                _client = _factory.CreateMqttClient();

                var optionsBuilder = new MqttClientOptionsBuilder()
                    .WithTcpServer(_settings.BrokerAddress, _settings.BrokerPort)
                    .WithClientId(_settings.ClientId)
                    .WithCredentials(_settings.Username, _settings.Password);

                if (_settings.UseTls)
                {
                    optionsBuilder.WithTlsOptions(o => { });
                }

                var options = optionsBuilder.Build();

                await _client.ConnectAsync(options);
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

    public async Task PublishAsync(string topic, string message)
    {
        await _retryPolicy.ExecuteAsync(async () =>
        {
            if (_client == null || !_client.IsConnected)
            {
                throw new InvalidOperationException("MQTT-asiakas ei ole yhdistetty");
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
        });
    }

    public async Task DisconnectAsync()
    {
        if (_client != null && _client.IsConnected)
        {
            try
            {
                await _client.DisconnectAsync();
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