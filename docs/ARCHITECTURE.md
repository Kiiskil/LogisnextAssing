# Arkkitehtuuridokumentaatio

## Yleiskuvaus

Järjestelmä koostuu kahdesta mikropalvelusta, jotka kommunikoivat keskenään MQTT-viestijonon välityksellä. Järjestelmä on suunniteltu skaalautuvaksi ja vikasietoiseksi.

## Komponentit

### OrderSubmissionService

- Vastaanottaa tilaukset REST-rajapinnan kautta
- Validoi tilaukset
- Generoi yksilölliset tilaus-ID:t
- Julkaisee tilaukset MQTT-jonoon
- Kerää metriikat Prometheukseen

Teknologiat:
- .NET 8.0
- MQTTnet
- Prometheus-net
- Polly (uudelleenyritykset)

### OrderProcessingService

- Kuuntelee MQTT-jonoa
- Käsittelee tilaukset
- Päivittää tilausten tilan
- Kerää metriikat Prometheukseen

Teknologiat:
- .NET 8.0
- MQTTnet
- Prometheus-net
- Polly (uudelleenyritykset)

### Infrastruktuuri

#### MQTT Broker (Eclipse Mosquitto)

- Toimii viestijonona palveluiden välillä
- Tukee QoS-tasoja (At least once)
- Mahdollisuus TLS-salaukseen
- Tukee pysyvää tallennusta

#### Prometheus

- Kerää metriikat molemmista palveluista
- Tukee hälytysten määrittelyä
- Tarjoaa PromQL-kyselykielen

#### Grafana

- Visualisoi Prometheuksen metriikat
- Tukee dashboardien luontia
- Mahdollisuus hälytysten konfigurointiin

## Tietoturva

### TLS-salaus

- MQTT-yhteydet voidaan salata TLS:llä
- Tuki sertifikaattien validoinnille
- Mahdollisuus käyttäjätunnistukseen

### Metriikat

- Prometheus-portit vain sisäverkossa
- Basic Authentication Grafanassa
- HTTPS-tuki Grafanalle

## Skaalautuvuus

### Horisontaalinen skaalaus

- OrderSubmissionService voidaan skaalata usealle instanssille
- OrderProcessingService voidaan skaalata usealle instanssille
- MQTT-broker tukee klusterointia

### Vikasietoisuus

- Uudelleenyrityslogiikka Polly-kirjastolla
- MQTT QoS-taso varmistaa viestien perillemenon
- Prometheus-metriikat auttavat ongelmien havaitsemisessa

## Monitorointi

### Metriikat

- Käsiteltyjen tilausten määrä
- Epäonnistuneiden tilausten määrä
- Tilausten käsittelyaika
- Jonon pituus
- Uudelleenyritysten määrä

### Lokitus

- Strukturoitu lokitus
- Eri lokitustasot (Info, Warning, Error)
- Mahdollisuus keskitettyyn lokienhallintaan

## Jatkokehitys

1. Integraatiotestit Docker-pohjaisella MQTT-brokerilla
2. End-to-end testit
3. Metriikoiden visualisointi Grafanassa
4. TLS-sertifikaattien käyttöönotto
5. Uudelleenyrityslogiikan laajentaminen
6. Keskitetty lokienhallinta
7. Automaattinen skaalaus 