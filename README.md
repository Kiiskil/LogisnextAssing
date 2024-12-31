# Logisnext Assignment - Real-Time Order Processing System

## Overview

The system consists of two microservices:
- **OrderSubmissionService**: Receives orders and publishes them to MQTT queue
- **OrderProcessingService**: Processes orders from MQTT queue

### Technologies and Libraries
- **MQTTnet (v4.3.1)**: For MQTT communication
- **Microsoft.Extensions.Logging**: For logging and diagnostics
- **System.Text.Json**: For JSON serialization
- **Polly**: For retry logic
- **Microsoft.Extensions.DependencyInjection**: For dependency injection

### Message Format
Orders are transmitted in JSON format:
```json
{
  "orderId": "string (GUID)",
  "customerName": "string",
  "productName": "string",
  "timestamp": "string (ISO 8601)",
  "status": "string (New/Processing/Processed)"
}
```

## Requirements

### Development Environment
- .NET 8.0 SDK
- Visual Studio 2022 or Visual Studio Code
- Git
- Docker (recommended for production use)

### Production Environment
- .NET 8.0 Runtime
- Mosquitto MQTT Broker

## Installation

### 1. Development Environment Setup

1. Install .NET 8.0 SDK:
   - Download and install: https://dotnet.microsoft.com/download/dotnet/8.0
   - Verify installation: `dotnet --version`

2. Install Mosquitto MQTT Broker:
   - Download package: https://mosquitto.org/download/
   - Windows: Download and install latest Windows installer
   - After installation:
     - Mosquitto service should start automatically
     - Check service status in Windows services (services.msc)
     - Ensure port 1883 is open and available

3. Clone repository:
   ```bash
   git clone [repository-url]
   cd LogisnextAssing
   ```

### 2. Development Version Launch

1. Start OrderProcessingService:
   ```bash
   cd src/OrderProcessingService
   dotnet run
   ```

2. Open new terminal and test OrderSubmissionService:
   ```bash
   cd src/OrderSubmissionService
   dotnet run order "Test Customer" "Product 123"
   ```

### 3. Production Version Publishing

1. Publish OrderProcessingService:
   ```bash
   cd src/OrderProcessingService
   # Windows x64 publish
   dotnet publish -c Release -r win-x64 --self-contained true
   # or Linux x64 publish
   dotnet publish -c Release -r linux-x64 --self-contained true
   ```

2. Publish OrderSubmissionService:
   ```bash
   cd src/OrderSubmissionService
   # Windows x64 publish
   dotnet publish -c Release -r win-x64 --self-contained true
   # or Linux x64 publish
   dotnet publish -c Release -r linux-x64 --self-contained true
   ```

Published versions can be found in:
- Windows: `bin/Release/net8.0/win-x64/publish/`
- Linux: `bin/Release/net8.0/linux-x64/publish/`

### 4. Production Version Launch and Usage

1. Copy published files to target environment:
   - Copy entire publish directory contents
   - Ensure `appsettings.json` is included
   - Maintain directory structure

2. Start OrderProcessingService:
   ```bash
   # Windows
   cd OrderProcessingService/bin/Release/net8.0/win-x64/publish
   ./OrderProcessingService.exe
   # or Linux
   cd OrderProcessingService/bin/Release/net8.0/linux-x64/publish
   ./OrderProcessingService
   ```

3. Submit order using OrderSubmissionService:
   ```bash
   # Windows syntax
   cd OrderSubmissionService/bin/Release/net8.0/win-x64/publish
   OrderSubmissionService.exe order "<customer name>" "<product name>"
   
   # Linux syntax
   cd OrderSubmissionService/bin/Release/net8.0/linux-x64/publish
   ./OrderSubmissionService order "<customer name>" "<product name>"
   
   # Example:
   OrderSubmissionService.exe order "John Smith" "Product ABC"
   ```

4. Check service status:
   - View console log messages
   - Check metrics at http://localhost:9090/metrics
   - Monitor Grafana dashboards at http://localhost:3000

Important notes:
- Ensure MQTT broker is running and accessible
- Check `appsettings.json` settings for target environment
- Firewall must allow configured ports (default 1883 for MQTT)

## Configuration

### MQTT Settings
MQTT settings are located in `appsettings.json` files:
- `src/OrderProcessingService/appsettings.json`
- `src/OrderSubmissionService/appsettings.json`

Default settings:
```json
{
  "MqttSettings": {
    "BrokerAddress": "localhost",
    "BrokerPort": 1883,
    "ClientId": "order-processing-service",
    "Username": "",
    "Password": "",
    "UseTls": false,
    "RetryPolicy": {
      "MaxRetries": 3,
      "DelaySeconds": 5
    }
  }
}
```

### Metrics Configuration
Metrics settings in `appsettings.json`:

```json
{
  "MetricsSettings": {
    "Port": 9090,
    "Path": "/metrics",
    "Prefix": "order_processing",
    "Tags": {
      "environment": "production",
      "service": "order-processing"
    },
    "EnableHealthCheck": true
  }
}
```

Key settings:
- `Port`: Prometheus metrics endpoint port
- `Path`: Metrics endpoint path
- `Prefix`: Metrics prefix in Prometheus
- `Tags`: Common tags for all metrics
- `EnableHealthCheck`: Enable health check endpoint

### Mosquitto Configuration
Mosquitto broker configuration file location:
- Windows: `C:\Program Files\mosquitto\mosquitto.conf`

Key settings:
```conf
max_keepalive 60
persistent_client_expiration 1h
max_inflight_messages 100
allow_anonymous true
listener 1883
```

## Monitoring and Metrics

### Prometheus and Grafana

The system uses Prometheus for metrics collection and Grafana for visualization:

1. Prometheus settings:
   ```yaml
   global:
     scrape_interval: 15s
     evaluation_interval: 15s

   scrape_configs:
     - job_name: 'order-processing'
       static_configs:
         - targets: ['localhost:9090']
     - job_name: 'order-submission'
       static_configs:
         - targets: ['localhost:9091']
   ```

2. Grafana dashboards:
   - Order processing times
   - Successful/failed orders
   - Queue length
   - Service status

### Collected Metrics

1. OrderProcessingService:
   - Number of processed orders
   - Number of failed orders
   - Order processing time (histogram)
   - Number of duplicate orders
   - MQTT connection status

2. OrderSubmissionService:
   - Number of submitted orders
   - Number of failed submissions
   - Submission time (histogram)
   - MQTT connection status

### Monitoring Setup

1. Start Prometheus:
   ```bash
   docker-compose up -d prometheus
   ```

2. Start Grafana:
   ```bash
   docker-compose up -d grafana
   ```

3. Open Grafana in browser:
   - URL: http://localhost:3000
   - Default credentials: admin/admin

4. Import ready-made dashboards:
   - Navigate to Dashboards > Import
   - Load dashboard definitions from `grafana/dashboards/`

### Alert Configuration

Grafana alerts can be configured for:
- High error rate (> 5% of orders)
- Long processing time (> 5s)
- MQTT connection loss
- Service failure

## Troubleshooting

### Common Issues

1. MQTT connection problems:
   - Verify Mosquitto service is running
   - Check port 1883 is open
   - Check firewall settings

2. Publishing errors:
   - Ensure you have write permissions to publish directory
   - Close all running instances before new publish

3. Runtime errors:
   - Verify .NET Runtime is installed
   - Check log files for error messages

## Testing

### Running Tests

Tests can be run using following commands:

```bash
# All tests
dotnet test

# Unit tests
dotnet test --filter Category=Unit

# Integration tests
dotnet test --filter Category=Integration
```

### Testing Strategy

1. Unit Tests:
   - Order validation
   - JSON serialization
   - Message formatting
   - Retry logic

2. Integration Tests:
   - MQTT connection establishment
   - Message publishing and receiving
   - Metrics collection

3. End-to-end Tests:
   - Order submission and processing
   - Error handling
   - Performance tests

### Test Example

```csharp
[Fact]
public void ValidateOrder_WithValidData_ShouldPass()
{
    // Arrange
    var order = new Order
    {
        OrderId = Guid.NewGuid().ToString(),
        CustomerName = "Test Customer",
        ProductName = "Test Product",
        Timestamp = DateTime.UtcNow,
        Status = OrderStatus.New
    };

    // Act
    var result = OrderValidator.Validate(order);

    // Assert
    Assert.True(result.IsValid);
}
```

## Scalability

### Running Multiple Instances

Services are designed to scale horizontally:

1. OrderSubmissionService:
   - Stateless service
   - Can run multiple parallel instances
   - Each instance gets unique client ID

2. OrderProcessingService:
   - Supports multiple parallel instances
   - Duplicate order handling prevented
   - Orders automatically distributed to available instances

Launch on different ports:

```bash
# OrderProcessingService instance 1
dotnet run --urls="http://localhost:5001"

# OrderProcessingService instance 2
dotnet run --urls="http://localhost:5002"
```

### Load Balancing

MQTT broker (Mosquitto) handles message distribution automatically:
- Messages distributed using round-robin principle
- Unprocessed messages remain in queue
- Messages delivered at least once (QoS 1) 