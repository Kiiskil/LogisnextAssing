using Prometheus;

namespace Common.Services;

public class PrometheusMetricsService : IMetricsService
{
    private readonly Counter _processedOrders = Metrics.CreateCounter("processed_orders_total", "Total number of processed orders");
    private readonly Counter _failedOrders = Metrics.CreateCounter("failed_orders_total", "Total number of failed orders");
    private readonly Counter _ordersCreated = Metrics.CreateCounter("created_orders_total", "Total number of created orders");
    private readonly Gauge _queueLength = Metrics.CreateGauge("order_queue_length", "Current length of the order queue");
    private readonly Histogram _processingDuration = Metrics.CreateHistogram("order_processing_duration_seconds", "Time taken to process orders");

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
} 