using Prometheus;

namespace Common.Services;

public class PrometheusMetricsService : IMetricsService
{
    private readonly Counter _processedOrders;
    private readonly Counter _failedOrders;
    private readonly Histogram _processingTime;
    private readonly Gauge _queueLength;

    public PrometheusMetricsService()
    {
        _processedOrders = Metrics.CreateCounter("orders_processed_total", "Total number of processed orders");
        _failedOrders = Metrics.CreateCounter("orders_failed_total", "Total number of failed orders");
        _processingTime = Metrics.CreateHistogram("order_processing_duration_seconds", 
            "Time taken to process orders",
            new HistogramConfiguration
            {
                Buckets = Histogram.ExponentialBuckets(0.1, 2, 10)
            });
        _queueLength = Metrics.CreateGauge("order_queue_length", "Current length of the order queue");
    }

    public void RecordProcessingTime(string orderId, TimeSpan duration)
    {
        _processingTime.Observe(duration.TotalSeconds);
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
} 