using Common.Models;
using Common.Services;
using Microsoft.Extensions.Logging;
using Moq;
using OrderSubmissionService.Services;
using Xunit;

namespace OrderSubmissionService.Tests;

public class OrderServiceTests
{
    private readonly Mock<IMqttPublisherService> _publisherMock;
    private readonly Mock<ILogger<OrderService>> _loggerMock;
    private readonly OrderService _orderService;

    public OrderServiceTests()
    {
        _publisherMock = new Mock<IMqttPublisherService>();
        _loggerMock = new Mock<ILogger<OrderService>>();
        _orderService = new OrderService(_publisherMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task CreateOrderAsync_ValidInput_CreatesAndPublishesOrder()
    {
        // Arrange
        var customerName = "Test Customer";
        var productName = "Test Product";

        // Act
        var result = await _orderService.CreateOrderAsync(customerName, productName);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(customerName, result.CustomerName);
        Assert.Equal(productName, result.ProductName);
        Assert.Equal(OrderStatus.New, result.Status);
        Assert.NotEmpty(result.OrderId);

        _publisherMock.Verify(x => x.PublishAsync(
            It.IsAny<string>(),
            It.Is<string>(msg => msg.Contains(result.OrderId))), 
            Times.Once);
    }

    [Theory]
    [InlineData("", "Product")]
    [InlineData("Customer", "")]
    [InlineData(null, "Product")]
    [InlineData("Customer", null)]
    public async Task CreateOrderAsync_InvalidInput_ThrowsArgumentException(string customerName, string productName)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _orderService.CreateOrderAsync(customerName, productName));

        _publisherMock.Verify(x => x.PublishAsync(
            It.IsAny<string>(), 
            It.IsAny<string>()), 
            Times.Never);
    }

    [Fact]
    public async Task CreateOrderAsync_PublishFails_ThrowsException()
    {
        // Arrange
        _publisherMock
            .Setup(x => x.PublishAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("Publishing failed"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => 
            _orderService.CreateOrderAsync("Customer", "Product"));
    }
} 