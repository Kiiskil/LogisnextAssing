using System.Text.Json;
using Common.Models;
using Common.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrderProcessingService.Services;
using OrderSubmissionService.Services;
using Xunit;

namespace EndToEndTests;

public class OrderFlowTests : IAsyncLifetime
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IMqttPublisherService _publisherService;
    private readonly IMqttSubscriberService _subscriberService;
    private readonly OrderService _orderService;
    private readonly OrderProcessor _processor;
    private readonly ILogger<OrderFlowTests> _logger;

    public OrderFlowTests()
    {
        var services = new ServiceCollection();
        
        // Konfiguroidaan palvelut testausta varten
        var mqttSettings = new MqttSettings
        {
            BrokerAddress = "localhost",
            BrokerPort = 1883,
            ClientId = $"test-client-{Guid.NewGuid()}",
            RetryPolicy = new RetryPolicy { MaxRetries = 3, DelaySeconds = 1 }
        };

        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<OrderFlowTests>();

        services.AddSingleton(mqttSettings);
        services.AddLogging(builder => builder.AddConsole());
        
        // Lisätään metriikkapalvelu
        var metricsService = new PrometheusMetricsService();
        services.AddSingleton<IMetricsService>(metricsService);

        // Käytetään samaa MQTT-yhteyttä kaikissa palveluissa
        var publisherLogger = loggerFactory.CreateLogger<MqttPublisherService>();
        var subscriberLogger = loggerFactory.CreateLogger<MqttSubscriberService>();
        var processorLogger = loggerFactory.CreateLogger<OrderProcessor>();

        var publisher = new MqttPublisherService(mqttSettings, publisherLogger, metricsService);
        var subscriber = new MqttSubscriberService(subscriberLogger, mqttSettings, metricsService);
        services.AddSingleton<IMqttPublisherService>(publisher);
        services.AddSingleton<IMqttSubscriberService>(subscriber);

        // Luodaan erilliset MQTT-palvelut OrderProcessorille
        var processorPublisher = new MqttPublisherService(
            new MqttSettings 
            { 
                BrokerAddress = mqttSettings.BrokerAddress,
                BrokerPort = mqttSettings.BrokerPort,
                ClientId = $"processor-publisher-{Guid.NewGuid()}",
                RetryPolicy = mqttSettings.RetryPolicy
            }, 
            publisherLogger, 
            metricsService);

        var processorSubscriber = new MqttSubscriberService(
            subscriberLogger,
            new MqttSettings 
            { 
                BrokerAddress = mqttSettings.BrokerAddress,
                BrokerPort = mqttSettings.BrokerPort,
                ClientId = $"processor-subscriber-{Guid.NewGuid()}",
                RetryPolicy = mqttSettings.RetryPolicy
            },
            metricsService);

        services.AddSingleton<OrderProcessor>(sp => new OrderProcessor(
            processorLogger,
            processorSubscriber,
            processorPublisher,
            metricsService));

        services.AddSingleton<OrderService>();

        _serviceProvider = services.BuildServiceProvider();

        _publisherService = _serviceProvider.GetRequiredService<IMqttPublisherService>();
        _subscriberService = _serviceProvider.GetRequiredService<IMqttSubscriberService>();
        _orderService = _serviceProvider.GetRequiredService<OrderService>();
        _processor = _serviceProvider.GetRequiredService<OrderProcessor>();
    }

    public async Task InitializeAsync()
    {
        _logger.LogInformation("Alustetaan testit...");
        
        try
        {
            // Yhdistetään MQTT-palveluihin
            await _publisherService.ConnectAsync();
            await _subscriberService.ConnectAsync();
            _logger.LogInformation("MQTT-yhteydet muodostettu");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Virhe MQTT-yhteyksien muodostamisessa");
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        _logger.LogInformation("Siivotaan testit...");
        
        try
        {
            await _processor.StopProcessingAsync();
            await _subscriberService.DisconnectAsync();
            await _publisherService.DisconnectAsync();
            _logger.LogInformation("MQTT-yhteydet suljettu");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Virhe MQTT-yhteyksien sulkemisessa");
        }

        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    [Fact]
    public async Task OrderProcessingEndToEnd_SucceedsCorrectly()
    {
        try
        {
            _logger.LogInformation("Starting end-to-end test execution");
            
            // Create order first to know its ID
            var order = CreateTestOrder();
            
            // Subscribe to processed messages for this order
            var receivedOrder = await SubscribeToProcessedOrder(order.OrderId);
            
            _logger.LogInformation("Processed order received: {OrderId}", receivedOrder.OrderId);
            
            await ValidateProcessedOrder(order, receivedOrder);
            
            // Start order processing
            await _processor.StartProcessingAsync();
            _logger.LogInformation("Order processing started");
            
            // Wait a moment to ensure order processing is running
            await Task.Delay(100);
            
            // Publish order for processing
            var orderJson = JsonSerializer.Serialize(order);
            await _publisherService.PublishAsync("orders/new", orderJson);
            _logger.LogInformation("Published new order via MQTT");
            
            // Wait for order processing
            await Task.Delay(1000);
            _logger.LogInformation("Order processing complete");
            
            Assert.NotNull(receivedOrder);
            Assert.Equal(order.OrderId, receivedOrder.OrderId);
            Assert.Equal(order.CustomerName, receivedOrder.CustomerName);
            Assert.Equal(order.ProductName, receivedOrder.ProductName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in end-to-end test");
            throw;
        }
        finally
        {
            await _processor.StopProcessingAsync();
            _logger.LogInformation("Order processing stopped");
        }
    }

    [Fact]
    public async Task OrderProcessing_FailsWithEmptyCustomer()
    {
        // Arrange
        var customerName = "";
        var productName = "Testi Tuote";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _orderService.CreateOrderAsync(customerName, productName));
    }

    [Fact]
    public async Task OrderProcessing_FailsWithEmptyProduct()
    {
        // Arrange
        var customerName = "Testi Asiakas";
        var productName = "";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _orderService.CreateOrderAsync(customerName, productName));
    }

    private Order CreateTestOrder()
    {
        return new Order
        {
            OrderId = Guid.NewGuid().ToString(),
            CustomerName = "Test Customer",
            ProductName = "Test Product",
            Status = OrderStatus.New,
            CreatedAt = DateTime.UtcNow,
            Timestamp = DateTime.UtcNow
        };
    }

    private async Task<Order> SubscribeToProcessedOrder(string orderId)
    {
        var processedOrder = new TaskCompletionSource<Order>();
        
        await _subscriberService.SubscribeAsync($"orders/processed/{orderId}", async message =>
        {
            _logger.LogInformation("Received message: {Message}", message);
            var receivedOrder = JsonSerializer.Deserialize<Order>(message);
            if (receivedOrder != null)
            {
                processedOrder.TrySetResult(receivedOrder);
            }
        });
        
        return await processedOrder.Task.WaitAsync(TimeSpan.FromSeconds(15));
    }

    private async Task ValidateProcessedOrder(Order original, Order processed)
    {
        Assert.NotNull(processed);
        Assert.Equal(original.OrderId, processed.OrderId);
        Assert.Equal(original.CustomerName, processed.CustomerName);
        Assert.Equal(original.ProductName, processed.ProductName);
        Assert.Equal(OrderStatus.Processed, processed.Status);
    }
}