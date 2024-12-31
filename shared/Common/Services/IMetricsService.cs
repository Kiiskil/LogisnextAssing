namespace Common.Services;

public interface IMetricsService
{
    void RecordProcessingTime(string orderId, TimeSpan duration);
    void IncrementProcessedOrders();
    void IncrementFailedOrders();
    void RecordQueueLength(int length);
    void IncrementOrdersCreated();
    void IncrementRetryAttempt(string operation);
    void RecordRetryDelay(string operation, TimeSpan delay);
} 