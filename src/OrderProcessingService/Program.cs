using Common.Models;
using Common.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderProcessingService.Services;
using Prometheus;

var builder = Host.CreateDefaultBuilder(args);

builder.ConfigureAppConfiguration((hostingContext, config) =>
{
    config.SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: false)
        .AddJsonFile($"appsettings.{hostingContext.HostingEnvironment.EnvironmentName}.json", optional: true)
        .AddEnvironmentVariables();
});

builder.ConfigureServices((hostContext, services) =>
{
    var mqttSettings = hostContext.Configuration.GetSection("MqttSettings").Get<MqttSettings>() 
        ?? throw new InvalidOperationException("MqttSettings missing from configuration");
    services.AddSingleton(mqttSettings);
    
    services.AddSingleton<IMetricsService, PrometheusMetricsService>();
    services.AddSingleton<IMqttSubscriberService, MqttSubscriberService>();
    services.AddSingleton<IMqttPublisherService, MqttPublisherService>();
    services.AddSingleton<OrderProcessor>();
    
    var metricsPort = hostContext.Configuration.GetValue<int>("Metrics:Port");
    var server = new MetricServer(port: metricsPort);
    server.Start();

    services.AddHealthChecks()
        .AddCheck<PrometheusMetricsService>("metrics_health");
});

var host = builder.Build();

try
{
    var processor = host.Services.GetRequiredService<OrderProcessor>();
    await processor.StartProcessingAsync();

    var cancellationTokenSource = new CancellationTokenSource();
    Console.CancelKeyPress += async (sender, e) =>
    {
        e.Cancel = true;
        cancellationTokenSource.Cancel();
        await processor.StopProcessingAsync();
    };

    await Task.Delay(-1, cancellationTokenSource.Token);
}
catch (OperationCanceledException)
{
    // Normaali sulkeminen
}
catch (Exception ex)
{
    var logger = host.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "Error processing orders");
    return 1;
}
finally
{
    await host.StopAsync();
}

return 0;
