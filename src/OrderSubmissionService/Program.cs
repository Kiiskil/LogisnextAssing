﻿using Common.Models;
using Common.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderSubmissionService.Services;
using Prometheus;
using System.Text.Json;

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
        ?? throw new InvalidOperationException("MqttSettings puuttuu konfiguraatiosta");
    services.AddSingleton(mqttSettings);
    
    services.AddSingleton<IMetricsService, PrometheusMetricsService>();
    services.AddSingleton<IMqttPublisherService, MqttPublisherService>();
    services.AddSingleton<OrderService>();
    
    var metricsPort = hostContext.Configuration.GetValue<int>("Metrics:Port");
    var server = new MetricServer(port: metricsPort);
    server.Start();
});

var host = builder.Build();

try
{
    if (args.Length != 3 || args[0] != "order")
    {
        Console.WriteLine("Käyttö: dotnet run -- order \"[Asiakkaan nimi]\" \"[Tuotteen nimi]\"");
        return 1;
    }

    var customerName = args[1];
    var productName = args[2];

    var orderService = host.Services.GetRequiredService<OrderService>();
    var mqttService = host.Services.GetRequiredService<IMqttPublisherService>();
    var logger = host.Services.GetRequiredService<ILogger<Program>>();

    await mqttService.ConnectAsync();

    var order = await orderService.CreateOrderAsync(customerName, productName);
    logger.LogInformation("Tilaus luotu. Tilauksen ID: {OrderId}", order.OrderId);

    // Julkaistaan tilaus
    await mqttService.PublishAsync("orders/new", JsonSerializer.Serialize(order));
    logger.LogInformation("Tilaus {OrderId} lähetetty käsittelyyn", order.OrderId);

    // Odotetaan valmistumista tai aikakatkaisua
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    try
    {
        // Odotetaan hetki, että tilaus ehtii käsittelyyn
        await Task.Delay(TimeSpan.FromSeconds(5), cts.Token);
        logger.LogInformation("Tilaus {OrderId} valmis", order.OrderId);
    }
    catch (OperationCanceledException)
    {
        logger.LogError("Tilauksen {OrderId} käsittely aikakatkaistiin", order.OrderId);
        return 1;
    }
    finally
    {
        await mqttService.DisconnectAsync();
    }

    return 0;
}
catch (Exception ex)
{
    var logger = host.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "Virhe tilauksen käsittelyssä");
    return 1;
}
finally
{
    await host.StopAsync();
}
