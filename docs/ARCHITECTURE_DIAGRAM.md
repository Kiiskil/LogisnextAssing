# System Architecture Diagram

## High-Level Architecture

```mermaid
graph TB
    subgraph "User"
        Client[Client/User]
    end

    subgraph "Order System"
        OSS[OrderSubmissionService]
        OPS[OrderProcessingService]
        MQTT[MQTT Broker]
        
        OSS -->|Publishes order| MQTT
        MQTT -->|Forwards order| OPS
        OPS -->|Updates status| MQTT
        MQTT -->|Order status| OSS
    end

    subgraph "Monitoring"
        Prometheus[Prometheus]
        Grafana[Grafana]
        
        OSS -->|Metrics| Prometheus
        OPS -->|Metrics| Prometheus
        Prometheus -->|Visualization| Grafana
    end

    Client -->|Creates order| OSS
    OSS -->|Order status| Client
```

## Service Internal Structure

### OrderSubmissionService

```mermaid
graph TB
    CLI[CLI Interface] -->|Input| Validator[Order Validation]
    Validator -->|Validated order| Publisher[MQTT Publisher]
    Publisher -->|Publish| RetryHandler[Retry Handler]
    RetryHandler -->|Publish| MQTT[MQTT Broker]
    Publisher -->|Metrics| Metrics[Metrics Service]
```

### OrderProcessingService

```mermaid
graph TB
    MQTT[MQTT Broker] -->|Order| Subscriber[MQTT Subscriber]
    Subscriber -->|Order| DupeCheck[Duplicate Check]
    DupeCheck -->|Unique order| Processor[Order Processing]
    Processor -->|Processed order| Publisher[MQTT Publisher]
    Processor -->|Metrics| Metrics[Metrics Service]
```

## Technical Implementation

- **Programming Language**: C# (.NET 8.0)
- **Messaging**: MQTT (MQTTnet 4.3.1)
- **Monitoring**: Prometheus + Grafana
- **Logging**: Microsoft.Extensions.Logging
- **Dependency Injection**: Microsoft.Extensions.DependencyInjection

## Data Flow

1. Client creates an order through OrderSubmissionService
2. OrderSubmissionService validates the order and generates a unique ID
3. Order is published to MQTT queue
4. OrderProcessingService receives the order and checks for duplicates
5. Order is processed and status is updated
6. Updated status is published back to MQTT queue
7. OrderSubmissionService receives the updated status
8. Metrics are collected to Prometheus and visualized in Grafana 