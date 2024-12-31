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
            throw new InvalidOperationException("Tilausten käsittely on jo käynnissä");
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
                        _logger.LogError("Virheellinen tilaus: {Message}", message);
                        await _publisher.PublishAsync("orders/error", "Error processing order: Invalid JSON");
                        _metrics.IncrementFailedOrders();
                        return;
                    }

                    // Tarkistetaan onko tilaus jo käsitelty
                    if (_processedOrders.TryGetValue(order.OrderId, out var processedTime))
                    {
                        if (DateTime.UtcNow - processedTime < TimeSpan.FromMinutes(5))
                        {
                            _logger.LogWarning("Tilaus {OrderId} on jo käsitelty {Minutes} minuuttia sitten", 
                                order.OrderId, (DateTime.UtcNow - processedTime).TotalMinutes);
                            return;
                        }
                    }

                    var startTime = DateTime.UtcNow;
                    _logger.LogInformation("Käsitellään tilausta: {OrderId}", order.OrderId);
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

                        _logger.LogInformation("Tilaus käsitelty: {OrderId}", order.OrderId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Virhe tilauksen julkaisussa");
                        await _publisher.PublishAsync("orders/error", $"Error processing order {order.OrderId}: {ex.Message}");
                        _metrics.IncrementFailedOrders();
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Virhe tilauksen käsittelyssä");
                    await _publisher.PublishAsync("orders/error", $"Error processing order: {ex.Message}");
                    _metrics.IncrementFailedOrders();
                }
            });

            _logger.LogInformation("Tilausten käsittely käynnistetty");
        }
        catch (Exception ex)
        {
            _isProcessing = false;
            _logger.LogError(ex, "Virhe tilausten käsittelyn käynnistyksessä");
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
            _logger.LogInformation("Tilausten käsittely pysäytetty");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Virhe tilausten käsittelyn pysäytyksessä");
            throw;
        }
    }
} 