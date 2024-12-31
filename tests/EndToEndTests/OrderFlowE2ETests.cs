using System.Net.Http.Json;
using Common.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace EndToEndTests;

public class OrderFlowE2ETests : IClassFixture<WebApplicationFactory<Startup>>
{
    private readonly WebApplicationFactory<Startup> _factory;

    public OrderFlowE2ETests(WebApplicationFactory<Startup> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateOrder_ShouldProcessSuccessfully()
    {
        // Arrange
        var client = _factory.CreateClient();
        var order = new Order
        {
            CustomerName = "TestiAsiakas",
            ProductName = "TestiTuote"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/orders", order);
        
        // Assert
        Assert.True(response.IsSuccessStatusCode);
        var createdOrder = await response.Content.ReadFromJsonAsync<Order>();
        Assert.NotNull(createdOrder);
        Assert.NotNull(createdOrder.OrderId);
        Assert.Equal(OrderStatus.New, createdOrder.Status);

        // Anna aikaa tilauksen käsittelylle
        await Task.Delay(2000);

        // Tarkista tilauksen tila
        var getResponse = await client.GetAsync($"/api/orders/{createdOrder.OrderId}");
        Assert.True(getResponse.IsSuccessStatusCode);
        
        var processedOrder = await getResponse.Content.ReadFromJsonAsync<Order>();
        Assert.NotNull(processedOrder);
        Assert.Equal(OrderStatus.Processed, processedOrder.Status);
    }

    [Fact]
    public async Task CreateOrder_WithInvalidData_ShouldFail()
    {
        // Arrange
        var client = _factory.CreateClient();
        var invalidOrder = new Order
        {
            CustomerName = "", // Tyhjä asiakasnimi
            ProductName = ""   // Tyhjä tuotenimi
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/orders", invalidOrder);
        
        // Assert
        Assert.False(response.IsSuccessStatusCode);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }
} 