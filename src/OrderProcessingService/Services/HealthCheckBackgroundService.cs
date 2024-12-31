using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OrderProcessingService.Services;

public class HealthCheckBackgroundService : BackgroundService
{
    private readonly HealthCheckService _healthCheckService;
    private readonly ILogger<HealthCheckBackgroundService> _logger;

    public HealthCheckBackgroundService(
        HealthCheckService healthCheckService,
        ILogger<HealthCheckBackgroundService> logger)
    {
        _healthCheckService = healthCheckService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = await _healthCheckService.CheckHealthAsync(stoppingToken);
                _logger.LogInformation(
                    "Health check completed with status: {Status}",
                    result.Status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running health check");
            }

            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }
} 