using Xunit;
using Moq;
using Common.Models;
using Common.Services;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using OrderProcessingService.Services;

namespace OrderProcessingService.Tests;

public class OrderProcessorTests
{
    private readonly Mock<ILogger<OrderProcessor>> _loggerMock;
    private readonly Mock<IMqttSubscriberService> _subscriberMock;
    private readonly Mock<IMqttPublisherService> _publisherMock;
    private readonly Mock<IMetricsService> _metricsMock;
    private readonly OrderProcessor _processor;

    public OrderProcessorTests()
    {
        _loggerMock = new Mock<ILogger<OrderProcessor>>();
        _subscriberMock = new Mock<IMqttSubscriberService>();
        _publisherMock = new Mock<IMqttPublisherService>();
        _metricsMock = new Mock<IMetricsService>();
        _processor = new OrderProcessor(_loggerMock.Object, _subscriberMock.Object, _publisherMock.Object, _metricsMock.Object);
    }

    [Fact]
    public async Task StartProcessingAsync_ShouldSubscribeToNewOrders()
    {
        // Arrange
        _subscriberMock.Setup(x => x.IsConnected).Returns(false);
        _publisherMock.Setup(x => x.IsConnected).Returns(false);
        _subscriberMock.Setup(x => x.ConnectAsync()).Returns(Task.CompletedTask);
        _publisherMock.Setup(x => x.ConnectAsync()).Returns(Task.CompletedTask);

        // Act
        await _processor.StartProcessingAsync();

        // Assert
        _subscriberMock.Verify(x => x.ConnectAsync(), Times.Once);
        _subscriberMock.Verify(x => x.SubscribeAsync("orders/new", It.IsAny<Action<string>>()), Times.Once);
    }

    [Fact]
    public async Task StartProcessingAsync_ShouldHandleConnectionFailure()
    {
        // Arrange
        _subscriberMock.Setup(x => x.IsConnected).Returns(false);
        _subscriberMock.Setup(x => x.ConnectAsync()).ThrowsAsync(new Exception("Connection failed"));
        _publisherMock.Setup(x => x.IsConnected).Returns(false);
        _publisherMock.Setup(x => x.ConnectAsync()).Returns(Task.CompletedTask);

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => _processor.StartProcessingAsync());
        _metricsMock.Verify(x => x.IncrementFailedOrders(), Times.Never);
    }

    [Fact]
    public async Task ProcessOrder_ShouldHandleDuplicateOrders()
    {
        // Arrange
        var order = new Order
        {
            OrderId = "test-id",
            CustomerName = "Test Customer",
            ProductName = "Test Product",
            Status = OrderStatus.New
        };
        var orderJson = JsonSerializer.Serialize(order);
        var messageHandled = new TaskCompletionSource<bool>();
        Action<string> messageHandler = null;

        _subscriberMock.Setup(x => x.IsConnected).Returns(false);
        _publisherMock.Setup(x => x.IsConnected).Returns(false);
        _subscriberMock.Setup(x => x.ConnectAsync()).Returns(Task.CompletedTask);
        _publisherMock.Setup(x => x.ConnectAsync()).Returns(Task.CompletedTask);
        _subscriberMock.Setup(x => x.SubscribeAsync("orders/new", It.IsAny<Action<string>>()))
            .Callback<string, Action<string>>((topic, handler) => messageHandler = handler)
            .Returns(Task.CompletedTask);
        _publisherMock.Setup(x => x.PublishAsync($"orders/processed/{order.OrderId}", It.IsAny<string>()))
            .Callback(() => messageHandled.SetResult(true))
            .Returns(Task.CompletedTask);

        // Act
        await _processor.StartProcessingAsync();
        messageHandler?.Invoke(orderJson); // Ensimmäinen käsittely
        await messageHandled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        messageHandler?.Invoke(orderJson); // Duplikaatti
        await Task.Delay(100); // Odotetaan hetki mahdollista toista käsittelyä

        // Assert
        _publisherMock.Verify(x => x.PublishAsync($"orders/processed/{order.OrderId}", It.IsAny<string>()), Times.Once);
        _metricsMock.Verify(x => x.IncrementProcessedOrders(), Times.Once);
    }

    [Fact]
    public async Task ProcessOrder_ShouldHandleInvalidJson()
    {
        // Arrange
        var invalidJson = "{ invalid json }";
        var errorHandled = new TaskCompletionSource<bool>();
        Action<string> messageHandler = null;

        _subscriberMock.Setup(x => x.IsConnected).Returns(false);
        _publisherMock.Setup(x => x.IsConnected).Returns(false);
        _subscriberMock.Setup(x => x.ConnectAsync()).Returns(Task.CompletedTask);
        _publisherMock.Setup(x => x.ConnectAsync()).Returns(Task.CompletedTask);
        _subscriberMock.Setup(x => x.SubscribeAsync("orders/new", It.IsAny<Action<string>>()))
            .Callback<string, Action<string>>((topic, handler) => messageHandler = handler)
            .Returns(Task.CompletedTask);
        _publisherMock.Setup(x => x.PublishAsync("orders/error", It.IsAny<string>()))
            .Callback(() => errorHandled.SetResult(true))
            .Returns(Task.CompletedTask);

        // Act
        await _processor.StartProcessingAsync();
        messageHandler?.Invoke(invalidJson);
        await errorHandled.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        _metricsMock.Verify(x => x.IncrementFailedOrders(), Times.Once);
        _publisherMock.Verify(x => x.PublishAsync("orders/error", It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task StopProcessingAsync_ShouldDisconnectServices()
    {
        // Arrange
        _subscriberMock.Setup(x => x.IsConnected).Returns(false);
        _publisherMock.Setup(x => x.IsConnected).Returns(false);
        _subscriberMock.Setup(x => x.ConnectAsync()).Returns(Task.CompletedTask);
        _publisherMock.Setup(x => x.ConnectAsync()).Returns(Task.CompletedTask);
        await _processor.StartProcessingAsync();

        // Act
        await _processor.StopProcessingAsync();

        // Assert
        _subscriberMock.Verify(x => x.DisconnectAsync(), Times.Once);
        _publisherMock.Verify(x => x.DisconnectAsync(), Times.Once);
    }
} 