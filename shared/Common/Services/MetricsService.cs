namespace Common.Services;

public class MetricsService : IMetricsService
{
    private int _processedOrders;
    private int _failedOrders;
    private int _queueLength;
    private int _ordersCreated;
    private readonly Dictionary<string, TimeSpan> _processingTimes = new();
    private readonly Dictionary<string, int> _retryAttempts = new();
    private readonly Dictionary<string, TimeSpan> _retryDelays = new();

    public void RecordProcessingTime(string orderId, TimeSpan duration)
    {
        _processingTimes[orderId] = duration;
    }

    public void IncrementProcessedOrders() => Interlocked.Increment(ref _processedOrders);

    public void IncrementFailedOrders() => Interlocked.Increment(ref _failedOrders);

    public void RecordQueueLength(int length) => _queueLength = length;

    public void IncrementOrdersCreated() => Interlocked.Increment(ref _ordersCreated);

    public void IncrementRetryAttempt(string operation)
    {
        lock (_retryAttempts)
        {
            if (!_retryAttempts.ContainsKey(operation))
                _retryAttempts[operation] = 0;
            _retryAttempts[operation]++;
        }
    }

    public void RecordRetryDelay(string operation, TimeSpan delay)
    {
        lock (_retryDelays)
        {
            _retryDelays[operation] = delay;
        }
    }
} 