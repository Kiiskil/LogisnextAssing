namespace Common.Models;

public class Order
{
    public string OrderId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public OrderStatus Status { get; set; }
}

public enum OrderStatus
{
    New,
    Processing,
    Processed,
    Failed
} 