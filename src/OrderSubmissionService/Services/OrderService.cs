using System.Text.Json;
using Common.Models;
using Common.Services;
using Microsoft.Extensions.Logging;

namespace OrderSubmissionService.Services;

public class OrderService
{
    private readonly IMqttPublisherService _publisherService;
    private readonly ILogger<OrderService> _logger;
    private const string NEW_ORDERS_TOPIC = "orders/new";

    public OrderService(IMqttPublisherService publisherService, ILogger<OrderService> logger)
    {
        _publisherService = publisherService;
        _logger = logger;
    }

    public async Task<Order> CreateOrderAsync(string customerName, string productName)
    {
        var order = new Order
        {
            OrderId = Guid.NewGuid().ToString(),
            CustomerName = customerName,
            ProductName = productName,
            Timestamp = DateTime.UtcNow,
            Status = OrderStatus.New
        };

        await ValidateOrderAsync(order);
        await PublishOrderAsync(order);

        _logger.LogInformation("Uusi tilaus luotu: {OrderId} asiakkaalle {CustomerName}", 
            order.OrderId, order.CustomerName);

        return order;
    }

    private async Task ValidateOrderAsync(Order order)
    {
        if (string.IsNullOrWhiteSpace(order.CustomerName))
        {
            throw new ArgumentException("Asiakkaan nimi ei voi olla tyhjä");
        }

        if (string.IsNullOrWhiteSpace(order.ProductName))
        {
            throw new ArgumentException("Tuotteen nimi ei voi olla tyhjä");
        }

        await Task.CompletedTask;
    }

    private async Task PublishOrderAsync(Order order)
    {
        try
        {
            var orderJson = JsonSerializer.Serialize(order);
            await _publisherService.PublishAsync(NEW_ORDERS_TOPIC, orderJson);
            _logger.LogInformation("Tilaus {OrderId} julkaistu jonoon", order.OrderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Virhe tilauksen {OrderId} julkaisussa", order.OrderId);
            throw;
        }
    }
} 