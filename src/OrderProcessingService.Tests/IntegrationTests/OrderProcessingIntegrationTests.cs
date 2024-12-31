using Xunit;
using Common.Models;
using Common.Services;
using Microsoft.Extensions.Logging;
using OrderProcessingService.Services;
using System.Text.Json;

namespace OrderProcessingService.Tests.IntegrationTests;

public class OrderProcessingIntegrationTests : IAsyncLifetime
{
    private readonly MqttSettings _settings;
    private readonly ILogger<MqttSubscriberService> _subscriberLogger;
    private readonly ILogger<MqttPublisherService> _publisherLogger;
    private readonly ILogger<OrderProcessor> _processorLogger;
    private readonly IMetricsService _metrics;
    private IMqttSubscriberService _subscriber;
    private IMqttPublisherService _publisher;
    private OrderProcessor _processor;

    private const int ProcessingTimeoutSeconds = 10; // Pidempi timeout simuloidun viiveen takia

    public OrderProcessingIntegrationTests()
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
        _processorLogger = loggerFactory.CreateLogger<OrderProcessor>();
        _metrics = new MetricsService();
    }

    public async Task InitializeAsync()
    {
        _subscriber = new MqttSubscriberService(_subscriberLogger, _settings, _metrics);
        _publisher = new MqttPublisherService(_publisherLogger, _settings, _metrics);
        _processor = new OrderProcessor(_processorLogger, _subscriber, _publisher, _metrics);

        await _subscriber.ConnectAsync();
        await _publisher.ConnectAsync();
        await _processor.StartProcessingAsync();

        // Odotetaan että yhteydet ovat valmiit
        await Task.Delay(1000);
    }

    public async Task DisposeAsync()
    {
        await _processor.StopProcessingAsync();
        await _subscriber.DisconnectAsync();
        await _publisher.DisconnectAsync();
    }

    [Fact]
    public async Task ProcessOrder_ShouldPublishProcessedOrder()
    {
        // Arrange
        var order = new Order
        {
            OrderId = Guid.NewGuid().ToString(),
            CustomerName = "Test Customer",
            ProductName = "Test Product",
            Status = OrderStatus.New
        };

        var orderJson = JsonSerializer.Serialize(order);
        var processedOrderReceived = new TaskCompletionSource<string>();

        // Act
        await _subscriber.SubscribeAsync($"orders/processed/{order.OrderId}", message =>
        {
            processedOrderReceived.SetResult(message);
        });

        await Task.Delay(100); // Odotetaan että tilaus on rekisteröity
        await _publisher.PublishAsync("orders/new", orderJson);

        // Assert
        var receivedMessage = await processedOrderReceived.Task.WaitAsync(TimeSpan.FromSeconds(ProcessingTimeoutSeconds));
        var processedOrder = JsonSerializer.Deserialize<Order>(receivedMessage);
        
        Assert.NotNull(processedOrder);
        Assert.Equal(order.OrderId, processedOrder.OrderId);
        Assert.Equal(OrderStatus.Processed, processedOrder.Status);
    }

    [Fact]
    public async Task ProcessOrder_ShouldHandleMultipleOrders()
    {
        // Arrange
        var orderCount = 5;
        var processedOrders = new List<Order>();
        var allOrdersProcessed = new TaskCompletionSource<bool>();

        // Act
        for (int i = 0; i < orderCount; i++)
        {
            var order = new Order
            {
                OrderId = Guid.NewGuid().ToString(),
                CustomerName = $"Customer {i}",
                ProductName = $"Product {i}",
                Status = OrderStatus.New
            };

            await _subscriber.SubscribeAsync($"orders/processed/{order.OrderId}", message =>
            {
                var processedOrder = JsonSerializer.Deserialize<Order>(message);
                lock (processedOrders)
                {
                    processedOrders.Add(processedOrder);
                    if (processedOrders.Count == orderCount)
                    {
                        allOrdersProcessed.SetResult(true);
                    }
                }
            });

            await Task.Delay(100); // Odotetaan että tilaus on rekisteröity
            await _publisher.PublishAsync("orders/new", JsonSerializer.Serialize(order));
        }

        // Assert
        await allOrdersProcessed.Task.WaitAsync(TimeSpan.FromSeconds(ProcessingTimeoutSeconds * 2));
        Assert.Equal(orderCount, processedOrders.Count);
        Assert.All(processedOrders, order => Assert.Equal(OrderStatus.Processed, order.Status));
    }

    [Fact]
    public async Task ProcessOrder_ShouldHandleInvalidJson()
    {
        // Arrange
        var invalidJson = "{ invalid json }";
        var errorReceived = new TaskCompletionSource<string>();

        // Act
        await _subscriber.SubscribeAsync("orders/error", message =>
        {
            errorReceived.SetResult(message);
        });

        await Task.Delay(100); // Odotetaan että tilaus on rekisteröity
        await _publisher.PublishAsync("orders/new", invalidJson);

        // Assert
        var errorMessage = await errorReceived.Task.WaitAsync(TimeSpan.FromSeconds(ProcessingTimeoutSeconds));
        Assert.Contains("Error processing order", errorMessage);
    }

    [Fact]
    public async Task ProcessOrder_ShouldUpdateMetrics()
    {
        // Arrange
        var order = new Order
        {
            OrderId = Guid.NewGuid().ToString(),
            CustomerName = "Test Customer",
            ProductName = "Test Product",
            Status = OrderStatus.New
        };

        var orderJson = JsonSerializer.Serialize(order);
        var processedOrderReceived = new TaskCompletionSource<string>();

        // Act
        await _subscriber.SubscribeAsync($"orders/processed/{order.OrderId}", message =>
        {
            processedOrderReceived.SetResult(message);
        });

        await Task.Delay(100); // Odotetaan että tilaus on rekisteröity
        var startTime = DateTime.UtcNow;
        await _publisher.PublishAsync("orders/new", orderJson);

        // Assert
        var receivedMessage = await processedOrderReceived.Task.WaitAsync(TimeSpan.FromSeconds(ProcessingTimeoutSeconds));
        var endTime = DateTime.UtcNow;
        var processedOrder = JsonSerializer.Deserialize<Order>(receivedMessage);
        
        Assert.NotNull(processedOrder);
        Assert.Equal(order.OrderId, processedOrder.OrderId);
        Assert.Equal(OrderStatus.Processed, processedOrder.Status);

        // Verify metrics
        var processingTime = endTime - startTime;
        Assert.True(processingTime.TotalMilliseconds >= 2000); // Vähintään simuloitu viive
    }
} 