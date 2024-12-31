namespace Common.Services;

public class MetricsService : IMetricsService
{
    private int _processedOrders;
    private int _failedOrders;
    private int _queueLength;
    private int _ordersCreated;
    private readonly Dictionary<string, TimeSpan> _processingTimes = new();

    public void RecordProcessingTime(string orderId, TimeSpan duration)
    {
        _processingTimes[orderId] = duration;
    }

    public void IncrementProcessedOrders() => Interlocked.Increment(ref _processedOrders);

    public void IncrementFailedOrders() => Interlocked.Increment(ref _failedOrders);

    public void RecordQueueLength(int length) => _queueLength = length;

    public void IncrementOrdersCreated() => Interlocked.Increment(ref _ordersCreated);
} 