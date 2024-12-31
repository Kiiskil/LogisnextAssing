using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using Polly;
using Polly.Retry;
using Common.Models;

namespace Common.Services;

public class MqttPublisherService : IMqttPublisherService
{
    private readonly MqttSettings _settings;
    private readonly ILogger<MqttPublisherService> _logger;
    private readonly IMetricsService _metricsService;
    private readonly IMqttClient _mqttClient;
    private readonly AsyncRetryPolicy _retryPolicy;

    public bool IsConnected => _mqttClient.IsConnected;

    public MqttPublisherService(MqttSettings settings, ILogger<MqttPublisherService> logger, IMetricsService metricsService)
    {
        _settings = settings;
        _logger = logger;
        _metricsService = metricsService;
        _mqttClient = new MqttFactory().CreateMqttClient();
        _retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                _settings.RetryPolicy.MaxRetries,
                retryAttempt => TimeSpan.FromSeconds(_settings.RetryPolicy.DelaySeconds * retryAttempt),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(exception, "Attempt {RetryCount} failed. Waiting {DelaySeconds} seconds before retrying.",
                        retryCount, timeSpan.TotalSeconds);
                });
    }

    public async Task ConnectAsync()
    {
        var optionsBuilder = new MqttClientOptionsBuilder()
            .WithTcpServer(_settings.BrokerAddress, _settings.BrokerPort)
            .WithClientId(_settings.ClientId)
            .WithCredentials(_settings.Username, _settings.Password);

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

        var options = optionsBuilder.Build();

        await _retryPolicy.ExecuteAsync(async () =>
        {
            await _mqttClient.ConnectAsync(options);
            _logger.LogInformation("Yhdistetty MQTT-brokeriin: {BrokerAddress}:{BrokerPort}", _settings.BrokerAddress, _settings.BrokerPort);
        });
    }

    public async Task DisconnectAsync()
    {
        if (_mqttClient.IsConnected)
        {
            await _mqttClient.DisconnectAsync();
            _logger.LogInformation("Yhteys MQTT-brokeriin katkaistu");
        }
    }

    public async Task PublishAsync(string topic, string message)
    {
        await _retryPolicy.ExecuteAsync(async () =>
        {
            if (!_mqttClient.IsConnected)
            {
                _logger.LogWarning("MQTT connection lost. Attempting to reconnect...");
                await ConnectAsync();
            }

            var messageObject = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(message)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .WithRetainFlag(false)
                .Build();

            await _mqttClient.PublishAsync(messageObject);
            _logger.LogInformation("Viesti julkaistu aiheeseen {Topic}", topic);
        });
    }
} 