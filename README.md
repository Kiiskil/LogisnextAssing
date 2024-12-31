# Logisnext Assignment - Real-Time Order Processing System

## Project Structure

```
/src
  /OrderSubmissionService     # Service for submitting orders
  /OrderProcessingService     # Service for processing orders
  /OrderProcessingService.Tests
/shared
  /Common                    # Shared components and interfaces
```

## Overview

The system consists of two microservices and a shared library:
- **OrderSubmissionService**: Receives orders and publishes them to MQTT queue
- **OrderProcessingService**: Processes orders from MQTT queue
- **Common**: Shared models, interfaces and services

### Technologies and Libraries
- **.NET 8.0**: Core framework
- **MQTTnet (v4.3.1)**: For MQTT communication
- **Microsoft.Extensions.Logging**: For logging and diagnostics
- **System.Text.Json**: For JSON serialization
- **prometheus-net**: For metrics collection
- **Microsoft.Extensions.DependencyInjection**: For dependency injection

### Message Format
Orders are transmitted in JSON format:
```json
{
  "orderId": "string (GUID)",
  "customerName": "string",
  "productName": "string",
  "createdAt": "string (ISO 8601)",
  "status": "string (New/Processing/Processed/Failed)"
}
```

## Requirements

### Development Environment
- .NET 8.0 SDK
- Visual Studio 2022 or Visual Studio Code
- Git
- Docker Desktop

### Infrastructure (Handled by Docker)
- MQTT Broker (Mosquitto)
- Prometheus (Metrics)
- Grafana (Dashboards)

## Installation

### Quick Start with Installation Script (Recommended)

The project includes an automated installation script that handles all setup and startup:

1. **Prerequisites**:
   - .NET 8.0 SDK
   - Docker Desktop
   - Administrator privileges (required for metrics collection)

2. **Run the Installation Script**:
   ```powershell
   .\install.ps1
   ```

   The script will:
   - Verify .NET SDK and Docker installations
   - Build .NET projects
   - Start required Docker containers:
     * MQTT Broker (Mosquitto) on port 1883
     * Prometheus (Metrics) on port 9090
     * Grafana (Dashboards) on port 3000
   - Launch the application services in separate windows

   > **Note**: The script requires administrator privileges and will automatically request elevation if needed.
   > No need to install MQTT broker separately - it's included in Docker containers!

3. **After Installation**:
   - Wait for both services to initialize
   - Order Processing Service will show "Connected to MQTT broker"
   - Order Submission Service will be ready for commands
   - Submit test orders using: `dotnet run order "Test Customer" "Product 123"`

4. **Monitor the System**:
   - Console output of both services
   - Metrics:
     * OrderSubmissionService: http://localhost:9100/metrics
     * OrderProcessingService: http://localhost:9101/metrics
     * Prometheus: http://localhost:9090 (aggregated metrics)
   - Dashboards: http://localhost:3000 (Grafana)

### Manual Installation (Alternative)

If you prefer manual installation:

1. Install .NET 8.0 SDK:
   - Download and install: https://dotnet.microsoft.com/download/dotnet/8.0
   - Verify installation: `dotnet --version`

2. Install Docker Desktop:
   - Download and install: https://www.docker.com/products/docker-desktop
   - Verify installation: `docker --version`

3. Clone repository:
   ```bash
   git clone [repository-url]
   cd LogisnextAssing
   ```

4. Start the services manually (requires administrator privileges):
   ```bash
   # Terminal 1: Start Order Processing Service
   cd src/OrderProcessingService
   dotnet run

   # Terminal 2: Start Order Submission Service
   cd src/OrderSubmissionService
   dotnet run
   ```

## Configuration

### MQTT Settings
MQTT settings are located in `appsettings.json` files and are preconfigured to work with the Docker-based MQTT broker:
- `src/OrderProcessingService/appsettings.json`
- `src/OrderSubmissionService/appsettings.json`

Default settings (no changes needed with installation script):
```json
{
  "MqttSettings": {
    "BrokerAddress": "localhost",
    "BrokerPort": 1883,
    "ClientId": "order-processing-service",
    "RetryPolicy": {
      "MaxRetries": 3,
      "DelaySeconds": 5
    }
  }
}
```

### Metrics Configuration
Metrics settings in `appsettings.json` for OrderProcessingService:

```json
{
  "Metrics": {
    "Port": 9101
  }
}
```

The system uses prometheus-net for exposing metrics. All metrics (including order creation metrics) are collected and exposed by the OrderProcessingService, which runs continuously in the background.

## Monitoring and Metrics

### Prometheus and Grafana

The system uses Prometheus for metrics collection and Grafana for visualization:

1. Prometheus settings:
   ```yaml
   global:
     scrape_interval: 15s
     evaluation_interval: 15s

   scrape_configs:
     - job_name: 'order_processing_service'
       static_configs:
         - targets: ['host.docker.internal:9101']
   ```

2. Grafana dashboards:
   - Order processing times
   - Successful/failed orders
   - Queue length
   - Service status

### Collected Metrics

All metrics are collected and exposed by OrderProcessingService:

1. Order Creation:
   - Number of created orders
   - Number of failed submissions

2. Order Processing:
   - Number of processed orders
   - Number of failed orders
   - Order processing time (histogram)
   - Number of duplicate orders
   - Service health status

### Example Prometheus Queries

Common PromQL queries for monitoring the system:

1. Order Creation Rate (orders/minute):
   ```promql
   rate(created_orders_total{job="order_processing_service"}[1m])
   ```

2. Failed Orders Percentage:
   ```promql
   (rate(failed_orders_total{job="order_processing_service"}[5m]) / rate(processed_orders_total{job="order_processing_service"}[5m])) * 100
   ```

3. Average Processing Time (last 5 minutes):
   ```promql
   rate(order_processing_duration_seconds_sum{job="order_processing_service"}[5m]) / rate(order_processing_duration_seconds_count{job="order_processing_service"}[5m])
   ```

4. Service Health Status (1 = healthy, 0 = unhealthy):
   ```promql
   service_health_status{job="order_processing_service"}
   ```

5. High Processing Time Alert:
   ```promql
   histogram_quantile(0.95, rate(order_processing_duration_seconds_bucket{job="order_processing_service"}[5m])) > 5
   ```

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
   - Default credentials:
     * Username: admin
     * Password: admin
   - **Note**: It is highly recommended to change the default password after first login for security purposes.

4. Monitor metrics directly:
   - OrderProcessingService metrics: http://localhost:9101/metrics
   - Prometheus: http://localhost:9090 (aggregated metrics)
   - Grafana: http://localhost:3000 (dashboards)

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