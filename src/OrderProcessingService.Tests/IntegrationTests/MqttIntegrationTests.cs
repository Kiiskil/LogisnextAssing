using Xunit;
using Common.Models;
using Common.Services;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using OrderProcessingService.Services;

namespace OrderProcessingService.Tests.IntegrationTests;

public class MqttIntegrationTests : IAsyncLifetime
{
    private readonly MqttSettings _settings;
    private readonly ILogger<MqttSubscriberService> _subscriberLogger;
    private readonly ILogger<MqttPublisherService> _publisherLogger;
    private readonly IMetricsService _metrics;
    private IMqttSubscriberService _subscriber;
    private IMqttPublisherService _publisher;

    public MqttIntegrationTests()
    {
        _settings = new MqttSettings
        {
            BrokerAddress = "localhost",
            BrokerPort = 1883,
            ClientId = $"test-client-{Guid.NewGuid()}",
            RetryPolicy = new RetryPolicy { MaxRetries = 3, DelaySeconds = 1 }
        };

        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _subscriberLogger = loggerFactory.CreateLogger<MqttSubscriberService>();
        _publisherLogger = loggerFactory.CreateLogger<MqttPublisherService>();
        _metrics = new MetricsService();
    }

    public async Task InitializeAsync()
    {
        _subscriber = new MqttSubscriberService(_subscriberLogger, _settings, _metrics);
        _publisher = new MqttPublisherService(_publisherLogger, _settings, _metrics);

        await _subscriber.ConnectAsync();
        await _publisher.ConnectAsync();
    }

    public async Task DisposeAsync()
    {
        await _subscriber.DisconnectAsync();
        await _publisher.DisconnectAsync();
    }

    [Fact]
    public async Task PublishAndSubscribe_ShouldDeliverMessage()
    {
        // Arrange
        var testTopic = $"test/topic/{Guid.NewGuid()}";
        var testMessage = "Test message";
        var messageReceived = new TaskCompletionSource<string>();

        // Act
        await _subscriber.SubscribeAsync(testTopic, message =>
        {
            messageReceived.SetResult(message);
        });

        await _publisher.PublishAsync(testTopic, testMessage);

        // Assert
        var receivedMessage = await messageReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(testMessage, receivedMessage);
    }

    [Fact]
    public async Task Subscribe_ShouldHandleMultipleMessages()
    {
        // Arrange
        var testTopic = $"test/topic/{Guid.NewGuid()}";
        var messageCount = 5;
        var receivedMessages = new List<string>();
        var allMessagesReceived = new TaskCompletionSource<bool>();

        // Act
        await _subscriber.SubscribeAsync(testTopic, message =>
        {
            receivedMessages.Add(message);
            if (receivedMessages.Count == messageCount)
            {
                allMessagesReceived.SetResult(true);
            }
        });

        for (int i = 0; i < messageCount; i++)
        {
            await _publisher.PublishAsync(testTopic, $"Message {i}");
        }

        // Assert
        await allMessagesReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(messageCount, receivedMessages.Count);
    }

    [Fact]
    public async Task Disconnect_ShouldHandleReconnection()
    {
        // Arrange
        var testTopic = $"test/topic/{Guid.NewGuid()}";
        var messageReceived = new TaskCompletionSource<string>();

        // Act
        await _subscriber.SubscribeAsync(testTopic, message =>
        {
            messageReceived.SetResult(message);
        });

        await _subscriber.DisconnectAsync();
        await _subscriber.ConnectAsync();

        await _publisher.PublishAsync(testTopic, "Test after reconnect");

        // Assert
        var receivedMessage = await messageReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("Test after reconnect", receivedMessage);
    }
} 