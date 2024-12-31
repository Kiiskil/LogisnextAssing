# Architecture Documentation

## Overview

The system consists of two microservices that communicate via an MQTT message queue. The system is designed to be scalable and fault-tolerant.

## Components

### OrderSubmissionService

- Receives orders through command line interface
- Validates orders
- Generates unique order IDs
- Publishes orders to MQTT queue
- Collects metrics to Prometheus

Technologies:
- .NET 8.0
- MQTTnet
- Prometheus-net
- Polly (for retries)

### OrderProcessingService

- Listens to MQTT queue
- Processes orders
- Updates order status
- Collects metrics to Prometheus

Technologies:
- .NET 8.0
- MQTTnet
- Prometheus-net
- Polly (for retries)

### Infrastructure

#### MQTT Broker (Eclipse Mosquitto)

- Acts as a message queue between services
- Supports QoS levels (At least once)
- Optional TLS encryption capability
- Optional user authentication
- Supports persistent storage

Configuration for clustering:
```conf
# Master node configuration
listener 1883
connection_messages true
allow_anonymous true

# Slave node configuration
connection master
address master-node:1883
topic # both 2
```

#### Prometheus

- Collects metrics from both services
- Supports alert configuration
- Provides PromQL query language

#### Grafana

- Visualizes Prometheus metrics
- Supports dashboard creation
- Alert configuration capability

## Security

### TLS Encryption (Optional)

- MQTT connections can be encrypted with TLS
- Support for certificate validation
- User authentication capability
- Configuration in appsettings.json:
```json
{
  "MqttSettings": {
    "UseTls": true,
    "CertificatePath": "/path/to/cert",
    "Username": "user",
    "Password": "pass"
  }
}
```

### Metrics

- Prometheus ports only in internal network
- Basic Authentication in Grafana
- HTTPS support for Grafana

## Scalability

### Horizontal Scaling

- OrderSubmissionService can be scaled to multiple instances
- OrderProcessingService can be scaled to multiple instances
- MQTT broker supports clustering with master-slave configuration

Example scaling configuration:
```bash
# Start multiple OrderProcessingService instances
dotnet run --urls="http://localhost:5001"
dotnet run --urls="http://localhost:5002"

# Each instance automatically gets unique client ID
# Duplicate order handling is implemented in OrderProcessor
```

### Fault Tolerance

- Retry logic with Polly library
- MQTT QoS level ensures message delivery
- Prometheus metrics help detect issues

## Monitoring

### Metrics

- Number of processed orders
- Number of failed orders
- Order processing time
- Queue length
- Number of retries

### Logging

- Structured logging
- Different log levels (Info, Warning, Error)
- Centralized log management capability

## Future Development

1. Integration tests with Docker-based MQTT broker
2. End-to-end tests
3. Metrics visualization in Grafana
4. TLS certificate implementation
5. Expansion of retry logic
6. Centralized log management
7. Automatic scaling 