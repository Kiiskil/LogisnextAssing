using Common.Models;
using Common.Services;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Collections.Concurrent;

namespace OrderProcessingService.Services;

public class OrderProcessor
{
    private readonly ILogger<OrderProcessor> _logger;
    private readonly IMqttSubscriberService _subscriber;
    private readonly IMqttPublisherService _publisher;
    private readonly IMetricsService _metrics;
    private bool _isProcessing;
    private readonly ConcurrentDictionary<string, DateTime> _processedOrders = new();

    public OrderProcessor(
        ILogger<OrderProcessor> logger,
        IMqttSubscriberService subscriber,
        IMqttPublisherService publisher,
        IMetricsService metrics)
    {
        _logger = logger;
        _subscriber = subscriber;
        _publisher = publisher;
        _metrics = metrics;
    }

    public async Task StartProcessingAsync()
    {
        if (_isProcessing)
        {
            throw new InvalidOperationException("Order processing is already running");
        }

        try
        {
            // Muodostetaan MQTT-yhteydet vain jos niitä ei ole vielä muodostettu
            if (!_subscriber.IsConnected)
            {
                await _subscriber.ConnectAsync();
            }
            if (!_publisher.IsConnected)
            {
                await _publisher.ConnectAsync();
            }
            
            _isProcessing = true;

            // Tilataan uudet tilaukset
            await _subscriber.SubscribeAsync("orders/new", async message =>
            {
                try
                {
                    var order = JsonSerializer.Deserialize<Order>(message);
                    if (order == null)
                    {
                        _logger.LogError("Invalid order: {Message}", message);
                        await _publisher.PublishAsync("orders/error", "Error processing order: Invalid JSON");
                        _metrics.IncrementFailedOrders();
                        return;
                    }

                    // Tarkistetaan onko tilaus jo käsitelty
                    if (_processedOrders.TryGetValue(order.OrderId, out var processedTime))
                    {
                        if (DateTime.UtcNow - processedTime < TimeSpan.FromMinutes(5))
                        {
                            _logger.LogWarning("Order {OrderId} was already processed {Minutes} minutes ago", 
                                order.OrderId, (DateTime.UtcNow - processedTime).TotalMinutes);
                            return;
                        }
                    }

                    var startTime = DateTime.UtcNow;
                    _logger.LogInformation("Processing order: {OrderId}", order.OrderId);
                    order.Status = OrderStatus.Processing;

                    try
                    {
                        // Simuloidaan käsittelyä
                        await Task.Delay(2000);

                        order.Status = OrderStatus.Processed;
                        await _publisher.PublishAsync($"orders/processed/{order.OrderId}", 
                            JsonSerializer.Serialize(order));

                        // Merkitään tilaus käsitellyksi
                        _processedOrders.TryAdd(order.OrderId, DateTime.UtcNow);
                        _metrics.IncrementProcessedOrders();
                        _metrics.RecordProcessingTime(order.OrderId, DateTime.UtcNow - startTime);

                        // Siivotaan vanhat tilaukset (yli 5 minuuttia vanhat)
                        foreach (var processedOrder in _processedOrders.ToList())
                        {
                            if (DateTime.UtcNow - processedOrder.Value > TimeSpan.FromMinutes(5))
                            {
                                _processedOrders.TryRemove(processedOrder.Key, out _);
                            }
                        }

                        _logger.LogInformation("Order processed: {OrderId}", order.OrderId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error publishing order");
                        await _publisher.PublishAsync("orders/error", $"Error processing order {order.OrderId}: {ex.Message}");
                        _metrics.IncrementFailedOrders();
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing order");
                    await _publisher.PublishAsync("orders/error", $"Error processing order: {ex.Message}");
                    _metrics.IncrementFailedOrders();
                }
            });

            _logger.LogInformation("Order processing started");
        }
        catch (Exception ex)
        {
            _isProcessing = false;
            _logger.LogError(ex, "Error starting order processing");
            throw;
        }
    }

    public async Task StopProcessingAsync()
    {
        if (!_isProcessing)
        {
            return;
        }

        try
        {
            await _subscriber.DisconnectAsync();
            await _publisher.DisconnectAsync();
            _isProcessing = false;
            _logger.LogInformation("Order processing stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping order processing");
            throw;
        }
    }
} 