# Real-Time Order Processing System

Tämä projekti on tilausten käsittelyjärjestelmä, joka koostuu kahdesta mikropalvelusta: tilausten vastaanotto- ja käsittelypalvelusta. Palvelut kommunikoivat keskenään MQTT-viestijonon välityksellä.

## Järjestelmävaatimukset

- .NET 8 SDK
- MQTT Broker (esim. Mosquitto)
- Windows/Linux/macOS käyttöjärjestelmä

## Asennus

1. Kloonaa repositorio:
```bash
git clone [repository-url]
cd LogisnextAssing
```

2. Asenna MQTT Broker (esimerkki Mosquitto):
- Windows: Lataa ja asenna [Mosquitto](https://mosquitto.org/download/)
- Linux: `sudo apt-get install mosquitto`
- macOS: `brew install mosquitto`

3. Rakenna projekti:
```bash
dotnet restore
dotnet build
```

## Palveluiden käynnistäminen

1. Käynnistä MQTT Broker:
- Windows: Käynnistä Mosquitto-palvelu
- Linux/macOS: `mosquitto`

2. Käynnistä Order Processing Service:
```bash
cd src/OrderProcessingService
dotnet run
```

3. Käynnistä Order Submission Service:
```bash
cd src/OrderSubmissionService
dotnet run -- order "Asiakas Oy" "Tuote123"
```

## Käyttö

### Tilauksen tekeminen
```bash
dotnet run -- order "[Asiakkaan nimi]" "[Tuotteen nimi]"
```

Esimerkki:
```bash
dotnet run -- order "Matti Meikäläinen" "Kannettava tietokone"
```

## Testien ajaminen

```bash
dotnet test
```

## Projektin rakenne

Katso tarkempi kuvaus projektin rakenteesta [teknisestä suunnitelmasta](docs/TECHNICAL_DESIGN.md).

## Arkkitehtuuri

Katso tarkempi arkkitehtuurikuvaus [arkkitehtuuridokumentista](docs/ARCHITECTURE.md).

## Kehitys

1. Forkkaa repositorio
2. Luo feature branch: `git checkout -b feature/uusi-ominaisuus`
3. Tee muutokset ja committaa: `git commit -am 'Lisää uusi ominaisuus'`
4. Työnnä branch: `git push origin feature/uusi-ominaisuus`
5. Tee Pull Request

## Lisenssi

MIT 