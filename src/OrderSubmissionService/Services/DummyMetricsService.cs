using Common.Services;

namespace OrderSubmissionService.Services;

public class DummyMetricsService : IMetricsService
{
    public void IncrementFailedOrders() { }
    public void IncrementOrdersCreated() { }
    public void IncrementProcessedOrders() { }
    public void IncrementRetryAttempt(string operation) { }
    public void RecordProcessingTime(string orderId, TimeSpan duration) { }
    public void RecordQueueLength(int length) { }
    public void RecordRetryDelay(string operation, TimeSpan delay) { }
} 