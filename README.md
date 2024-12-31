# Logisnext Assignment - Real-Time Order Processing System

## Yleiskuvaus

Järjestelmä koostuu kahdesta mikropalvelusta:
- **OrderSubmissionService**: Vastaanottaa tilauksia ja julkaisee ne MQTT-jonoon
- **OrderProcessingService**: Käsittelee tilauksia MQTT-jonosta

### Käytetyt teknologiat ja kirjastot
- **MQTTnet (v4.3.1)**: MQTT-kommunikaatioon
- **Microsoft.Extensions.Logging**: Lokitukseen ja diagnostiikkaan
- **System.Text.Json**: JSON-serialisointiin
- **Polly**: Uudelleenyrityslogiikkaan
- **Microsoft.Extensions.DependencyInjection**: Riippuvuuksien injektointiin

### Viestiformaatti
Tilaukset välitetään JSON-muodossa:
```json
{
  "orderId": "string (GUID)",
  "customerName": "string",
  "productName": "string",
  "timestamp": "string (ISO 8601)",
  "status": "string (New/Processing/Processed)"
}
```

## Vaatimukset

### Kehitysympäristö
- .NET 8.0 SDK
- Visual Studio 2022 tai Visual Studio Code
- Git

### Tuotantoympäristö
- .NET 8.0 Runtime
- Mosquitto MQTT Broker

## Asennus

### 1. Kehitysympäristön asennus

1. Asenna .NET 8.0 SDK:
   - Lataa ja asenna: https://dotnet.microsoft.com/download/dotnet/8.0
   - Varmista asennus: `dotnet --version`

2. Asenna Mosquitto MQTT Broker:
   - Lataa asennuspaketti: https://mosquitto.org/download/
   - Windows: Lataa ja asenna uusin versio Windows-asennuspaketista
   - Asennuksen jälkeen:
     - Mosquitto-palvelun pitäisi käynnistyä automaattisesti
     - Tarkista palvelun tila Windowsin palveluista (services.msc)
     - Varmista että portti 1883 on auki ja käytettävissä

3. Kloonaa repositorio:
   ```bash
   git clone [repository-url]
   cd LogisnextAssing
   ```

### 2. Kehitysversion käynnistys

1. Käynnistä OrderProcessingService:
   ```bash
   cd src/OrderProcessingService
   dotnet run
   ```

2. Avaa uusi terminaali ja testaa OrderSubmissionService:
   ```bash
   cd src/OrderSubmissionService
   dotnet run order "Testi Asiakas" "Tuote 123"
   ```

### 3. Tuotantoversion julkaisu

1. Julkaise OrderProcessingService:
   ```bash
   cd src/OrderProcessingService
   dotnet publish -c Release -r win-x64 --self-contained true
   ```

2. Julkaise OrderSubmissionService:
   ```bash
   cd src/OrderSubmissionService
   dotnet publish -c Release -r win-x64 --self-contained true
   ```

### 4. Tuotantoversion käynnistys

1. Käynnistä OrderProcessingService:
   ```bash
   cd src/OrderProcessingService/bin/Release/net8.0/win-x64/publish
   ./OrderProcessingService.exe
   ```

2. Lähetä tilaus OrderSubmissionService:llä:
   ```bash
   # Syntaksi: OrderSubmissionService.exe order "<asiakkaan nimi>" "<tuotteen nimi>"
   cd src/OrderSubmissionService/bin/Release/net8.0/win-x64/publish
   ./OrderSubmissionService.exe order "Asiakkaan Nimi" "Tuotteen Nimi"
   ```

## Konfigurointi

### MQTT-asetukset
Palveluiden MQTT-asetukset löytyvät `appsettings.json`-tiedostoista:
- `src/OrderProcessingService/appsettings.json`
- `src/OrderSubmissionService/appsettings.json`

Oletusasetukset:
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

### Metriikoiden konfigurointi
Metriikoiden asetukset löytyvät `appsettings.json`-tiedostoista:

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

Tärkeimmät asetukset:
- `Port`: Prometheus metrics endpoint portti
- `Path`: Metrics endpointin polku
- `Prefix`: Metriikkojen etuliite Prometheuksessa
- `Tags`: Yleiset tagit kaikille metriikoille
- `EnableHealthCheck`: Health check -endpointin käyttöönotto

### Mosquitto-konfiguraatio
Mosquitto-brokerin konfiguraatio löytyy tiedostosta:
- Windows: `C:\Program Files\mosquitto\mosquitto.conf`

Tärkeimmät asetukset:
```conf
max_keepalive 60
persistent_client_expiration 1h
max_inflight_messages 100
allow_anonymous true
listener 1883
```

## Monitorointi ja metriikat

### Prometheus ja Grafana

Järjestelmä käyttää Prometheusta metriikoiden keräämiseen ja Grafanaa niiden visualisointiin:

1. Prometheus-asetukset:
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

2. Grafana-dashboardit:
   - Tilausten käsittelyajat
   - Onnistuneet/epäonnistuneet tilaukset
   - Jonon pituus
   - Palveluiden tila

### Kerättävät metriikat

1. OrderProcessingService:
   - Käsiteltyjen tilausten määrä
   - Epäonnistuneiden tilausten määrä
   - Tilausten käsittelyaika (histogrammi)
   - Duplikaattitilausten määrä
   - MQTT-yhteyden tila

2. OrderSubmissionService:
   - Lähetettyjen tilausten määrä
   - Epäonnistuneiden lähetysten määrä
   - Lähetysaika (histogrammi)
   - MQTT-yhteyden tila

### Monitoroinnin käyttöönotto

1. Käynnistä Prometheus:
   ```bash
   docker-compose up -d prometheus
   ```

2. Käynnistä Grafana:
   ```bash
   docker-compose up -d grafana
   ```

3. Avaa Grafana selaimessa:
   - URL: http://localhost:3000
   - Oletustunnukset: admin/admin

4. Tuo valmiit dashboardit:
   - Navigoi Dashboards > Import
   - Lataa dashboard-määritykset kansiosta `grafana/dashboards/`

### Hälytysten konfigurointi

Grafanassa voidaan määrittää hälytykset esimerkiksi:
- Korkea virhemäärä (> 5% tilauksista)
- Pitkä käsittelyaika (> 5s)
- MQTT-yhteyden katkeaminen
- Palvelun kaatuminen

## Vianetsintä

### Yleiset ongelmat

1. MQTT-yhteysongelmat:
   - Varmista että Mosquitto-palvelu on käynnissä
   - Tarkista että portti 1883 on auki
   - Tarkista palomuuriasetukset

2. Julkaisuvirheet:
   - Varmista että sinulla on kirjoitusoikeudet julkaisukansioon
   - Sulje kaikki ajossa olevat instanssit ennen uutta julkaisua

3. Suoritusvirheet:
   - Tarkista että .NET Runtime on asennettu
   - Tarkista lokitiedostot virheilmoitusten varalta

## Arkkitehtuuri

### Komponenttidiagrammi
```
┌─────────────────┐     ┌──────────────┐     ┌──────────────────┐
│     Order       │     │              │     │      Order       │
│   Submission    │────▶│    MQTT      │────▶│    Processing    │
│    Service      │     │    Broker    │     │     Service      │
└─────────┬───────┘     │  (Mosquitto) │     └────────┬─────────┘
          │             │              │              │
          │             └──────────────┘              │
          │                                          │
          │             ┌──────────────┐             │
          └────────────▶│  Prometheus  │◀────────────┘
                       │              │
                       └───────┬──────┘
                              │
                       ┌──────┴──────┐
                       │   Grafana   │
                       │             │
                       └─────────────┘
```

### Prosessikuvaus
1. OrderSubmissionService:
   - Vastaanottaa tilaukset komentoriviparametreina
   - Validoi tilaukset
   - Julkaisee tilaukset MQTT-jonoon (topic: orders/new)
   - Odottaa kunnes tilaus on julkaistu ja näyttää tilauksen ID:n
   - Prosessointiaika: < 1 sekunti

2. OrderProcessingService:
   - Kuuntelee uusia tilauksia MQTT-jonosta
   - Käsittelee tilaukset (2 sekunnin simuloitu viive per tilaus)
   - Julkaisee käsitellyt tilaukset takaisin MQTT-jonoon (topic: orders/processed/{orderId})
   - Estää duplikaattitilausten käsittelyn
   - Prosessointiaika: 2 sekuntia per tilaus

3. MQTT-broker (Mosquitto):
   - Toimii viestinvälittäjänä palveluiden välillä
   - Varmistaa viestien luotettavan toimituksen (QoS 1)
   - Tukee QoS-tasoja 0-2

### Skaalautuvuus ja vikasietoisuus
Järjestelmä tukee useita rinnakkaisia instansseja:
- OrderSubmissionService voi skaalautua horisontaalisesti, koska jokainen instanssi on tilaton
- OrderProcessingService voi skaalautua horisontaalisesti:
  - Duplikaattitilausten käsittely on estetty
  - Jokainen instanssi saa uniikin client ID:n
  - Tilaukset jaetaan automaattisesti vapaana oleville instansseille
- MQTT-broker voidaan klusteroida korkean saatavuuden varmistamiseksi
  - Tukee master-slave replikointia
  - Tukee bridge-konfiguraatiota hajautettuun toimintaan

## Testaus

Järjestelmän testaus on toteutettu seuraavilla tasoilla:

1. Yksikkötestit:
   - Tilauksen validointi
   - JSON-serialisointi
   - Viestien muotoilu

2. Integraatiotestit:
   - MQTT-yhteyden muodostus
   - Viestien julkaisu ja vastaanotto
   - Retry-politiikan toiminta

3. End-to-end testit:
   - Tilauksen lähetys ja käsittely
   - Virhetilanteiden käsittely

Testit voi ajaa komennolla:
```bash
dotnet test
``` 