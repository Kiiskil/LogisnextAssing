# Real-Time Order Processing System - Tekninen suunnitelma

## Projektin rakenne

```
LogisnextAssing/
├── src/
│   ├── OrderSubmissionService/
│   │   ├── Program.cs
│   │   ├── Services/
│   │   │   ├── OrderService.cs
│   │   │   └── MqttPublisherService.cs
│   │   ├── Models/
│   │   │   └── Order.cs
│   │   └── OrderSubmissionService.csproj
│   │
│   └── OrderProcessingService/
│       ├── Program.cs
│       ├── Services/
│       │   ├── OrderProcessingService.cs
│       │   └── MqttSubscriberService.cs
│       ├── Models/
│       │   └── Order.cs
│       └── OrderProcessingService.csproj
│
├── tests/
│   ├── OrderSubmissionService.Tests/
│   └── OrderProcessingService.Tests/
│
└── shared/
    └── Common/
        ├── Models/
        │   └── Order.cs
        └── Common.csproj
```

## Teknologiavalinnat

### Ohjelmointikieli ja framework
- C# 12
- .NET 8
- Microsoft.Extensions.DependencyInjection
- Microsoft.Extensions.Logging

### Viestijonotekniikka
- MQTTnet 4.3.1.873
- MQTTnet.Extensions.ManagedClient

### Testaus
- xUnit
- Moq
- FluentAssertions

## Luokkarakenne

### Order-malli
```csharp
public class Order
{
    public string OrderId { get; set; }
    public string CustomerName { get; set; }
    public string ProductName { get; set; }
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
```

### Palvelurajapinnat

#### IOrderService
```csharp
public interface IOrderService
{
    Task<Order> CreateOrderAsync(string customerName, string productName);
    Task<bool> ValidateOrderAsync(Order order);
}
```

#### IMqttPublisherService
```csharp
public interface IMqttPublisherService
{
    Task ConnectAsync();
    Task PublishAsync(string topic, string message);
    Task DisconnectAsync();
}
```

#### IOrderProcessingService
```csharp
public interface IOrderProcessingService
{
    Task ProcessOrderAsync(Order order);
    Task UpdateOrderStatusAsync(Order order, OrderStatus status);
}
```

## Konfiguraatio

### MQTT-asetukset
```json
{
    "MqttSettings": {
        "BrokerAddress": "localhost",
        "BrokerPort": 1883,
        "ClientId": "service-name",
        "Username": "",
        "Password": "",
        "UseTls": false
    }
}
```

## Virheenkäsittely

### Poikkeusluokat
```csharp
public class OrderValidationException : Exception
{
    public OrderValidationException(string message) : base(message) { }
}

public class MqttConnectionException : Exception
{
    public MqttConnectionException(string message, Exception innerException) 
        : base(message, innerException) { }
}
```

## Lokitus

### Lokitustasot
- Information: Normaalit operaatiot
- Warning: Lievät virhetilanteet
- Error: Vakavat virheet
- Debug: Kehitysaikaiset lokitukset

## Testausstrategia

### Yksikkötestit
- OrderService-luokan testit
- Validointilogiikan testit
- MQTT-palveluiden mock-testit

### Integraatiotestit
- Viestijonon toiminnan testaus
- Palveluiden välisen kommunikaation testaus

### End-to-end testit
- Koko tilausputken testaus
- Virhetilanteiden testaus

## Suorituskyky ja skaalautuvuus

### Viestijonon asetukset
- QoS-taso: 1 (At least once)
- Keep-alive: 60 sekuntia
- Clean session: true

### Rinnakkaisuus
- Async/await kaikkialla
- Säikeistys tarvittaessa
- Tilaton toteutus 