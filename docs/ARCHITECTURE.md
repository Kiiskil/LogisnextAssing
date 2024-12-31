# Real-Time Order Processing System - Arkkitehtuurikuvaus

## Yleiskuvaus
Järjestelmä koostuu kahdesta mikropalvelusta, jotka kommunikoivat keskenään viestijonon välityksellä. Järjestelmä on suunniteltu skaalautuvaksi ja modulaariseksi.

## Komponentit

### 1. Order Submission Service
- Konsolisovellus, joka vastaanottaa tilauksia
- Julkaisee tilaukset viestijonoon
- Käsittelee tilausten validoinnin ja ID:n generoinnin
- Vastaa tilaustietojen formaatin oikeellisuudesta

### 2. Order Processing Service
- Konsolisovellus, joka käsittelee tilauksia
- Kuuntelee viestijonoa uusien tilausten varalta
- Prosessoi tilaukset ja päivittää niiden tilan
- Lokittaa käsitellyt tilaukset

### 3. Message Queue (MQTT)
- Keskitetty viestinvälitysjärjestelmä
- Käytetään MQTTnet-kirjastoa
- Mahdollistaa asynkronisen kommunikaation palveluiden välillä
- Tukee useita yhtäaikaisia julkaisijoita ja tilaajia

## Tekninen toteutus

### Viestiformaatti
```json
{
    "orderId": "string",
    "customerName": "string",
    "productName": "string",
    "timestamp": "datetime",
    "status": "string"
}
```

### Viestijonon topicit
- orders/new: Uudet tilaukset
- orders/processed: Käsitellyt tilaukset

## Skaalautuvuus
- Palvelut ovat tilattomia ja voidaan skaalata horisontaalisesti
- Viestijonon avulla voidaan käsitellä useita tilauksia rinnakkain
- Useita Order Processing Service -instansseja voi kuunnella samaa jonoa

## Virheenkäsittely
- Palvelut käsittelevät yhteysongelmat viestijonoon
- Epäonnistuneet tilaukset lokitetaan
- Uudelleenyrityslogiikka viestijonoyhteyksille

## Monitorointi
- Palvelut tuottavat lokitietoja toiminnastaan
- Tilausten käsittelyaikoja seurataan
- Viestijonon tilaa monitoroidaan

## Tietoturva
- Viestijonoyhteydet suojataan TLS:llä
- Viestit validoidaan ennen käsittelyä
- Käyttäjäsyötteet sanitoidaan

## Testaus
- Yksikkötestit komponenteille
- Integraatiotestit palveluiden väliselle kommunikaatiolle
- End-to-end testit koko tilausputkelle 