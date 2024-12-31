using System.Text.Json;
using Common.Models;
using Common.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace OrderProcessingService.Tests;

public class OrderProcessingServiceTests
{
    private readonly Mock<IMqttSubscriberService> _subscriberMock;
    private readonly Mock<IMqttPublisherService> _publisherMock;
    private readonly Mock<ILogger<Services.OrderProcessingService>> _loggerMock;
    private readonly Services.OrderProcessingService _processingService;
    private Action<string>? _messageHandler;

    public OrderProcessingServiceTests()
    {
        _subscriberMock = new Mock<IMqttSubscriberService>();
        _publisherMock = new Mock<IMqttPublisherService>();
        _loggerMock = new Mock<ILogger<Services.OrderProcessingService>>();

        _subscriberMock
            .Setup(x => x.SubscribeAsync(It.IsAny<string>(), It.IsAny<Action<string>>()))
            .Callback<string, Action<string>>((topic, handler) => _messageHandler = handler);

        _processingService = new Services.OrderProcessingService(
            _subscriberMock.Object,
            _publisherMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task StartProcessingAsync_SubscribesToCorrectTopic()
    {
        // Act
        await _processingService.StartProcessingAsync();

        // Assert
        _subscriberMock.Verify(x => x.ConnectAsync(), Times.Once);
        _publisherMock.Verify(x => x.ConnectAsync(), Times.Once);
        _subscriberMock.Verify(
            x => x.SubscribeAsync("orders/new", It.IsAny<Action<string>>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleNewOrder_ProcessesAndPublishesOrder()
    {
        // Arrange
        var order = new Order
        {
            OrderId = "test-123",
            CustomerName = "Test Customer",
            ProductName = "Test Product",
            Status = OrderStatus.New
        };
        var orderJson = JsonSerializer.Serialize(order);

        await _processingService.StartProcessingAsync();

        // Act
        _messageHandler?.Invoke(orderJson);
        await Task.Delay(2500); // Odotetaan käsittelyn valmistumista

        // Assert
        _publisherMock.Verify(x => x.PublishAsync(
            "orders/processed",
            It.Is<string>(msg => msg.Contains("\"Status\":2"))), // OrderStatus.Processing = 2
            Times.Once);
    }

    [Fact]
    public async Task HandleNewOrder_InvalidJson_LogsError()
    {
        // Arrange
        var invalidJson = "invalid json";
        await _processingService.StartProcessingAsync();

        // Act
        _messageHandler?.Invoke(invalidJson);
        await Task.Delay(100);

        // Assert
        _publisherMock.Verify(
            x => x.PublishAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public async Task StopProcessingAsync_DisconnectsServices()
    {
        // Act
        await _processingService.StopProcessingAsync();

        // Assert
        _subscriberMock.Verify(x => x.DisconnectAsync(), Times.Once);
        _publisherMock.Verify(x => x.DisconnectAsync(), Times.Once);
    }
} 