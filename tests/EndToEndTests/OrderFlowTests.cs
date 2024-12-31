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
    public async Task TilauksenKäsittelyPäästäPäähän_OnnistuuOikein()
    {
        // Arrange
        var customerName = "Testi Asiakas";
        var productName = "Testi Tuote";
        var processedOrder = new TaskCompletionSource<Order>();
        
        try
        {
            _logger.LogInformation("Aloitetaan päästä-päähän testin suoritus");

            // Luodaan ensin tilaus, jotta tiedetään sen ID
            var order = await _orderService.CreateOrderAsync(customerName, productName);
            _logger.LogInformation("Luotu uusi tilaus: {OrderId}", order.OrderId);

            // Tilataan processed-viestit tälle tilaukselle
            await _subscriberService.SubscribeAsync($"orders/processed/{order.OrderId}", async message =>
            {
                _logger.LogInformation("Vastaanotettu viesti: {Message}", message);
                var receivedOrder = JsonSerializer.Deserialize<Order>(message);
                if (receivedOrder != null)
                {
                    _logger.LogInformation("Käsitelty tilaus vastaanotettu: {OrderId}", receivedOrder.OrderId);
                    processedOrder.TrySetResult(receivedOrder);
                }
            });
            _logger.LogInformation("Tilattu processed-viestit");

            // Käynnistetään tilausten käsittely
            await _processor.StartProcessingAsync();
            _logger.LogInformation("Tilausten käsittely käynnistetty");

            // Odotetaan hetki että tilausten käsittely on varmasti käynnissä
            await Task.Delay(1000);

            // Julkaistaan tilaus käsiteltäväksi
            var orderJson = JsonSerializer.Serialize(order);
            await _publisherService.PublishAsync("orders/new", orderJson);
            _logger.LogInformation("Julkaistu uusi tilaus MQTT:llä");

            // Odotetaan tilauksen käsittelyä
            var result = await processedOrder.Task.WaitAsync(TimeSpan.FromSeconds(15));
            _logger.LogInformation("Tilauksen käsittely valmis");
            
            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result.OrderId);
            Assert.Equal(customerName, result.CustomerName);
            Assert.Equal(productName, result.ProductName);
            Assert.Equal(OrderStatus.Processed, result.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Virhe päästä-päähän testissä");
            throw;
        }
        finally
        {
            await _processor.StopProcessingAsync();
            _logger.LogInformation("Tilausten käsittely pysäytetty");
        }
    }

    [Fact]
    public async Task TilauksenKäsittely_EpäonnistuuTyhjälläAsiakkaalla()
    {
        // Arrange
        var customerName = "";
        var productName = "Testi Tuote";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _orderService.CreateOrderAsync(customerName, productName));
    }

    [Fact]
    public async Task TilauksenKäsittely_EpäonnistuuTyhjälläTuotteella()
    {
        // Arrange
        var customerName = "Testi Asiakas";
        var productName = "";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _orderService.CreateOrderAsync(customerName, productName));
    }
}