# Tekninen dokumentaatio

## Palveluiden rakenne

### OrderSubmissionService

#### Komponentit

1. `Program.cs`
   - Konfiguraation lataus
   - DI-konttien rekisteröinti
   - Metriikoiden alustus
   - Palvelun elinkaaren hallinta

2. `MqttPublisherService`
   - MQTT-yhteyden hallinta
   - Viestien julkaisu
   - Uudelleenyrityslogiikka
   - Metriikoiden keräys

3. `OrderService`
   - Tilausten validointi
   - ID:n generointi
   - Tilausten muotoilu

#### Konfiguraatio

```json
{
  "MqttSettings": {
    "BrokerAddress": "localhost",
    "BrokerPort": 1883,
    "ClientId": "order-submission-service",
    "Username": "",
    "Password": "",
    "UseTls": false,
    "RetryPolicy": {
      "MaxRetries": 3,
      "DelaySeconds": 5
    }
  },
  "Metrics": {
    "Enabled": true,
    "Port": 9100
  }
}
```

### OrderProcessingService

#### Komponentit

1. `Program.cs`
   - Konfiguraation lataus
   - DI-konttien rekisteröinti
   - Metriikoiden alustus
   - Palvelun elinkaaren hallinta

2. `MqttSubscriberService`
   - MQTT-yhteyden hallinta
   - Viestien vastaanotto
   - Uudelleenyrityslogiikka
   - Metriikoiden keräys

3. `OrderProcessingService`
   - Tilausten käsittely
   - Tilan päivitys
   - Virheenkäsittely

#### Konfiguraatio

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
  },
  "Metrics": {
    "Enabled": true,
    "Port": 9101
  }
}
```

### Common-kirjasto

#### Models

1. `Order`
   - Tilauksen perustiedot
   - Validointisäännöt

2. `MqttSettings`
   - MQTT-konfiguraatio
   - TLS-asetukset
   - Uudelleenyritysasetukset

3. `RetryPolicy`
   - Uudelleenyritysasetukset
   - Viiveet ja yrityskerrat

#### Services

1. `IMetricsService`
   - Metriikoiden rajapinta
   - Tilausten seuranta
   - Suorituskyvyn mittaus

2. `PrometheusMetricsService`
   - Prometheus-metriikat
   - Laskurit ja mittarit
   - Histogrammit

## Infrastruktuuri

### Docker-kontit

1. MQTT Broker
   ```yaml
   mqtt:
     image: eclipse-mosquitto:latest
     ports:
       - "1883:1883"
       - "9001:9001"
     volumes:
       - ./mosquitto/config:/mosquitto/config
       - ./mosquitto/data:/mosquitto/data
       - ./mosquitto/log:/mosquitto/log
   ```

2. Prometheus
   ```yaml
   prometheus:
     image: prom/prometheus:latest
     ports:
       - "9090:9090"
     volumes:
       - ./prometheus:/etc/prometheus
     command:
       - '--config.file=/etc/prometheus/prometheus.yml'
   ```

3. Grafana
   ```yaml
   grafana:
     image: grafana/grafana:latest
     ports:
       - "3000:3000"
     environment:
       - GF_SECURITY_ADMIN_PASSWORD=admin
     volumes:
       - ./grafana:/var/lib/grafana
   ```

### Prometheus-konfiguraatio

```yaml
global:
  scrape_interval: 15s
  evaluation_interval: 15s

scrape_configs:
  - job_name: 'order_submission_service'
    static_configs:
      - targets: ['host.docker.internal:9100']

  - job_name: 'order_processing_service'
    static_configs:
      - targets: ['host.docker.internal:9101']
```

## Testaus

### Yksikkötestit

1. OrderSubmissionService.Tests
   - Tilausten validointi
   - MQTT-julkaisu
   - Metriikat

2. OrderProcessingService.Tests
   - Tilausten käsittely
   - MQTT-tilaus
   - Metriikat

### Integraatiotestit

1. MQTT-integraatio
   - Viestien välitys
   - QoS-tasot
   - Uudelleenyritykset

2. Metriikat
   - Prometheus-integraatio
   - Mittareiden tarkkuus

## Jatkokehitys

1. Integraatiotestit Docker-pohjaisella MQTT-brokerilla
2. End-to-end testit
3. Metriikoiden visualisointi Grafanassa
4. TLS-sertifikaattien käyttöönotto
5. Uudelleenyrityslogiikan laajentaminen
6. Keskitetty lokienhallinta
7. Automaattinen skaalaus 