# Architecture Documentation

## Overview

The system consists of two microservices and a shared library that communicate via an MQTT message queue. The system is designed to be scalable and fault-tolerant.

## Components

### OrderSubmissionService

- Receives orders through command line interface
- Validates orders
- Generates unique order IDs
- Publishes orders to MQTT queue
- Collects metrics using prometheus-net

Technologies:
- .NET 8.0
- MQTTnet
- prometheus-net
- Custom retry policy

### OrderProcessingService

- Listens to MQTT queue
- Processes orders
- Updates order status
- Collects metrics using prometheus-net
- Handles duplicate orders with 5-minute window

Technologies:
- .NET 8.0
- MQTTnet
- prometheus-net
- Custom retry policy

### Common Library

- Shared models (Order, OrderStatus)
- Shared interfaces (IMqttPublisherService, IMqttSubscriberService)
- Shared services (MetricsService)
- Configuration models (MqttSettings, RetryPolicy)

### Infrastructure

#### MQTT Broker (Eclipse Mosquitto)

- Acts as a message queue between services
- Supports QoS levels (At least once)
- Simple configuration without authentication
- Default port 1883

Configuration:
```conf
listener 1883
allow_anonymous true
```

#### Prometheus

- Collects metrics from both services
- Metrics exposed on configured ports
- Basic metrics: processed orders, failed orders, processing time

## Security

Currently implemented:
- Basic error handling
- Duplicate order detection
- Input validation

Planned future improvements:
- TLS encryption for MQTT
- Authentication for MQTT
- Metrics endpoint security

## Scalability

### Current Implementation

- OrderSubmissionService is command-line tool that can be run multiple times
- OrderProcessingService handles duplicate orders with 5-minute window
- MQTT broker handles message distribution

### Fault Tolerance

- Custom retry policy for MQTT connections
- Error handling and logging
- Metrics for monitoring failures

## Monitoring

### Metrics

Currently implemented:
- Number of processed orders
- Number of failed orders
- Order processing time
- Number of created orders

### Logging

- Structured logging with Microsoft.Extensions.Logging
- Different log levels (Info, Warning, Error)
- Console output

## Future Development

1. Docker support for services
2. Integration tests with Docker-based MQTT broker
3. End-to-end tests
4. Metrics visualization
5. TLS and authentication for MQTT
6. Expanded retry logic
7. Health checks implementation 