using System.Text.Json;
using Common.Models;
using Common.Services;
using Microsoft.Extensions.Logging;

namespace OrderSubmissionService.Services;

public class OrderService : IOrderService
{
    private readonly IMqttPublisherService _mqttPublisher;
    private readonly ILogger<OrderService> _logger;

    public OrderService(IMqttPublisherService mqttPublisher, ILogger<OrderService> logger)
    {
        _mqttPublisher = mqttPublisher;
        _logger = logger;
    }

    public async Task<Order> CreateOrderAsync(string customerName, string productName)
    {
        if (string.IsNullOrWhiteSpace(customerName))
            throw new ArgumentException("Customer name cannot be empty", nameof(customerName));

        if (string.IsNullOrWhiteSpace(productName))
            throw new ArgumentException("Product name cannot be empty", nameof(productName));

        var order = new Order
        {
            OrderId = Guid.NewGuid().ToString(),
            CustomerName = customerName,
            ProductName = productName,
            Status = OrderStatus.New,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            await _mqttPublisher.PublishAsync("orders/new", JsonSerializer.Serialize(order));
            _logger.LogInformation("Order {OrderId} created and sent for processing", order.OrderId);
            return order;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating order {OrderId}", order.OrderId);
            throw;
        }
    }
} 