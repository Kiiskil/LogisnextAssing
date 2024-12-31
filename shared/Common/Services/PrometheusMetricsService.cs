using Prometheus;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Common.Services;

public class PrometheusMetricsService : IMetricsService, IHealthCheck
{
    private readonly Counter _processedOrders = Metrics.CreateCounter("processed_orders_total", "Total number of processed orders");
    private readonly Counter _failedOrders = Metrics.CreateCounter("failed_orders_total", "Total number of failed orders");
    private readonly Counter _ordersCreated = Metrics.CreateCounter("created_orders_total", "Total number of created orders");
    private readonly Gauge _queueLength = Metrics.CreateGauge("order_queue_length", "Current length of the order queue");
    private readonly Histogram _processingDuration = Metrics.CreateHistogram("order_processing_duration_seconds", "Time taken to process orders");
    private readonly Counter _retryAttempts = Metrics.CreateCounter("retry_attempts_total", "Total number of retry attempts", new CounterConfiguration
    {
        LabelNames = new[] { "operation" }
    });
    private readonly Histogram _retryDelays = Metrics.CreateHistogram("retry_delay_seconds", "Time waited between retries", new HistogramConfiguration
    {
        LabelNames = new[] { "operation" },
        Buckets = Histogram.ExponentialBuckets(0.1, 2, 8)
    });
    private readonly Gauge _serviceHealth = Metrics.CreateGauge("service_health_status", "Current health status of the service (1 = healthy, 0 = unhealthy)");

    public void RecordProcessingTime(string orderId, TimeSpan duration)
    {
        _processingDuration.Observe(duration.TotalSeconds);
    }

    public void IncrementProcessedOrders()
    {
        _processedOrders.Inc();
    }

    public void IncrementFailedOrders()
    {
        _failedOrders.Inc();
    }

    public void RecordQueueLength(int length)
    {
        _queueLength.Set(length);
    }

    public void IncrementOrdersCreated()
    {
        _ordersCreated.Inc();
    }

    public void IncrementRetryAttempt(string operation)
    {
        _retryAttempts.WithLabels(operation).Inc();
    }

    public void RecordRetryDelay(string operation, TimeSpan delay)
    {
        _retryDelays.WithLabels(operation).Observe(delay.TotalSeconds);
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var failureRate = _failedOrders.Value / (_processedOrders.Value + 1) * 100;
            var isHealthy = failureRate < 5 && _queueLength.Value < 1000;

            _serviceHealth.Set(isHealthy ? 1 : 0);

            var data = new Dictionary<string, object>
            {
                { "processed_orders", _processedOrders.Value },
                { "failed_orders", _failedOrders.Value },
                { "queue_length", _queueLength.Value },
                { "failure_rate", failureRate }
            };

            return isHealthy 
                ? HealthCheckResult.Healthy("Service is healthy", data)
                : HealthCheckResult.Degraded("Service is degraded", null, data);
        }
        catch (Exception ex)
        {
            _serviceHealth.Set(0);
            return HealthCheckResult.Unhealthy("Health check failed", ex);
        }
    }
} 