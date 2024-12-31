using System.Text.Json;
using Common.Models;
using Common.Services;
using Microsoft.Extensions.Logging;

namespace OrderProcessingService.Services;

public class OrderProcessingService
{
    private readonly IMqttSubscriberService _subscriberService;
    private readonly IMqttPublisherService _publisherService;
    private readonly ILogger<OrderProcessingService> _logger;
    private const string NEW_ORDERS_TOPIC = "orders/new";
    private const string PROCESSED_ORDERS_TOPIC = "orders/processed";

    public OrderProcessingService(
        IMqttSubscriberService subscriberService, 
        IMqttPublisherService publisherService,
        ILogger<OrderProcessingService> logger)
    {
        _subscriberService = subscriberService;
        _publisherService = publisherService;
        _logger = logger;
    }

    public async Task StartProcessingAsync()
    {
        await _subscriberService.ConnectAsync();
        await _publisherService.ConnectAsync();
        await _subscriberService.SubscribeAsync(NEW_ORDERS_TOPIC, HandleNewOrderAsync);
        _logger.LogInformation("Tilausten käsittely aloitettu");
    }

    private async void HandleNewOrderAsync(string orderJson)
    {
        try
        {
            var order = JsonSerializer.Deserialize<Order>(orderJson);
            if (order == null)
            {
                _logger.LogError("Tilauksen deserialisointi epäonnistui");
                return;
            }

            _logger.LogInformation("Käsitellään tilausta: {OrderId}", order.OrderId);

            // Simuloidaan tilauksen käsittelyä 2 sekunnin viiveellä
            await Task.Delay(2000);

            order.Status = OrderStatus.Processed;
            _logger.LogInformation("Tilaus {OrderId} käsitelty", order.OrderId);

            // Julkaistaan käsitelty tilaus
            var processedOrderJson = JsonSerializer.Serialize(order);
            await _publisherService.PublishAsync(PROCESSED_ORDERS_TOPIC, processedOrderJson);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Virhe tilauksen JSON-käsittelyssä");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Virhe tilauksen käsittelyssä");
        }
    }

    public async Task StopProcessingAsync()
    {
        await _subscriberService.DisconnectAsync();
        await _publisherService.DisconnectAsync();
        _logger.LogInformation("Tilausten käsittely lopetettu");
    }
} 