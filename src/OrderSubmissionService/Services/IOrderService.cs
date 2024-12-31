using Common.Models;

namespace OrderSubmissionService.Services;

public interface IOrderService
{
    Task<Order> CreateOrderAsync(string customerName, string productName);
} 