namespace Common.Models;

public class RetryPolicy
{
    public int MaxRetries { get; set; } = 3;
    public int DelaySeconds { get; set; } = 5;
} 