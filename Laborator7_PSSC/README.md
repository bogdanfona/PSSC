# Laborator 7 - Comunicare sincrona

API-ul cosului de cumparaturi din Laboratorul 6 apeleaza, la finalul procesarii comenzii (dupa plata), un API nou care reprezinta **contextul de livrari**. Apelul trece printr-un client HTTP tipizat cu politica de reincercare din `Microsoft.Extensions.Http.Resilience`: orice eroare tranzitorie se reincearca de 3 ori, la intervale exponentiale.

Analiza solutiei din `Exemple` (Sarcina 1) este in [Sarcina1.md](Sarcina1.md).

## Fluxul

```
client ──POST /carts/{id}/pay──> Laborator7_PSSC.Api ────────────────> SQL Server (cosul devine Paid)
                                        │
                                        └─POST /deliveries──> Laborator7_PSSC.Delivery
                                          (DeliveryApiClient,     (AWB, data estimata)
                                           3 reincercari: ~0,6 s, 1,2 s, 2,4 s + jitter)
```

1. `PayCartWorkflow` (din Lab 6) marcheaza cosul platit in baza.
2. Cosul platit este recitit si transformat in contractul `DeliveryRequest`.
3. `DeliveryApiClient` il trimite la `POST /deliveries`. Raspunsul `DeliveryConfirmation` (AWB, data estimata) se intoarce clientului impreuna cu cosul.
4. Daca API-ul de livrari nu raspunde nici dupa 3 reincercari, plata ramane salvata si raspunsul este **502** cu un mesaj clar; livrarea se poate reprograma cu `POST /carts/{id}/delivery`.

## Endpoint-uri

API-ul cosului (`https://localhost:7197/swagger`) - cele din Lab 6, plus:

| Metoda si ruta | Ce face | Raspunsuri |
|---|---|---|
| `POST /carts/{id}/pay` | Plateste cosul **si programeaza livrarea**; raspunsul contine `cart` si `delivery` | 200, 404, 409, 502 |
| `POST /carts/{id}/delivery` | Reprogrameaza livrarea unui cos platit (cand la plata livrarile nu au raspuns) | 200, 404, 409 (cos neplatit), 502 |

API-ul de livrari (`https://localhost:7297/swagger`):

| Metoda si ruta | Ce face | Raspunsuri |
|---|---|---|
| `POST /deliveries` | Programeaza livrarea unui cos; o cerere repetata pentru acelasi cos intoarce livrarea existenta | 201 (noua), 200 (existenta), 400, 503 (simulat) |
| `GET /deliveries` | Toate livrarile | 200 |
| `GET /deliveries/{cartId}` | Livrarea unui cos | 200, 404 |

## Structura

| Proiect / fisier | Rol |
|---|---|
| `Laborator7_PSSC.Contracts/` | **Nou.** DTO-urile schimbate intre API-uri: `DeliveryRequest`, `DeliveryItem`, `DeliveryConfirmation`. Fara logica si fara referinte |
| `Laborator7_PSSC.Delivery/` | **Nou.** API-ul contextului de livrari: `DeliveryStore` (in memorie), `FailureSimulator`, endpoint-urile |
| `Laborator7_PSSC.Api/Clients/` | **Nou.** `DeliveryApiClient` (client tipizat), `DeliveryApiOptions`, `AddDeliveryApiClient` (politica de reincercare) |
| `Laborator7_PSSC.Api/Mapping/DeliveryMapping.cs` | **Nou.** Cosul platit -> `DeliveryRequest` |
| `Laborator7_PSSC.Api/Endpoints/CartsEndpoints.cs` | Plata apeleaza livrarile; endpoint nou `POST /carts/{id}/delivery` |
| `Laborator7_PSSC.Api.Tests/` | **Nou.** Testele politicii de reincercare, fara retea |
| `Functional`, `Domain`, `Data`, `Domain.Tests` | Copiate din Lab 6, neschimbate |

## Politica de reincercare

In `Laborator7_PSSC.Api/Clients/ServiceCollectionExtensions.cs`:

```csharp
services.AddHttpClient<DeliveryApiClient>(...)
    .AddResilienceHandler("delivery-api", (pipeline, context) =>
    {
        pipeline.AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = 3,                        // din DeliveryApi:Retry:MaxRetryAttempts
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            Delay = TimeSpan.FromMilliseconds(600),      // din DeliveryApi:Retry:BaseDelayMilliseconds
        });
        pipeline.AddTimeout(TimeSpan.FromSeconds(5));    // pe fiecare incercare
    });
```

- **Erori tranzitorii** (cele implicite din `HttpRetryStrategyOptions`): raspunsuri 5xx, 408, 429, `HttpRequestException` (server oprit, conexiune refuzata), expirarea unei incercari. Un 400 nu se reincearca.
- **Intervale exponentiale**: 600 ms, 1,2 s, 2,4 s, cu *jitter* (variatie aleatoare, ca mai multi clienti sa nu reincerce simultan).
- **Configurare, nu cod**: adresa, numarul de reincercari, intarzierea de baza si limita pe incercare sunt in sectiunea `DeliveryApi` din `appsettings.json`, validate la pornire (`ValidateOnStart`).

## Decizii de proiectare

- **Livrarea dupa plata**: un cos se mai poate schimba cat timp este neplatit; dupa plata, comanda este finala si poate fi livrata.
- **Contracte intr-un proiect separat**: ambele API-uri referentiaza `Laborator7_PSSC.Contracts`; niciun proiect web nu il referentiaza pe celalalt (altfel endpoint-urile livrarilor ar aparea si in API-ul cosului).
- **Contextul de livrari are datele lui**: nu acceseaza baza magazinului; tine livrarile in memorie (se pierd la repornire - suficient pentru laborator).
- **Reincercarea unui `POST` este sigura**: API-ul de livrari pastreaza o singura livrare per cos (`CartId`), deci o cerere repetata de politica de reincercare (de exemplu dupa un timeout in care serverul a apucat totusi sa creeze livrarea) intoarce aceeasi livrare, nu una dubla.
- **Esecul livrarilor nu se ascunde si nu anuleaza plata**: plata este deja salvata cand se apeleaza livrarile. Raspunsul este 502 (dependenta nu raspunde), cu un mesaj care spune ce s-a intamplat si cum se reia; `POST /carts/{id}/delivery` reprogrameaza livrarea. Exemplul cursului lasa exceptia sa devina 500; aici se raporteaza explicit.
- **Reincercarile se pot vedea**: in Development, `Delivery:FailFirstAttempts = 2` face API-ul de livrari sa raspunda 503 la primele doua incercari pentru fiecare cos. In consola API-ului cosului apar evenimentele Polly `OnRetry`, iar in consola livrarilor `attempt 1 fails on purpose`, `attempt 2 fails on purpose`, apoi livrarea la incercarea 3. Cu valoarea `4` se epuizeaza reincercarile si se obtine 502; cu `0` nu esueaza niciodata.
- **Teste fara retea**: `DeliveryApiClientRetryTests` inregistreaza clientul exact ca in `Program.cs`, dar cu un `HttpMessageHandler` fals care numara incercarile: 503 de doua ori apoi succes (3 incercari), mereu 500 (4 incercari, apoi exceptie), 408/429/502/eroare de retea (reincercate), 400 (nereincercat).

## Pornire

Necesita configurarea din Laboratorul 4 (Docker Desktop, `Laborator4_PSSC/.env`, connection string-ul in user-secrets) si baza din Laboratorul 6. Toate laboratoarele folosesc acelasi `UserSecretsId`.

Cele doua API-uri comunica prin HTTPS, deci certificatul de dezvoltare trebuie sa fie de incredere (o singura data):

```bash
dotnet dev-certs https --trust
```

Fara el, fiecare apel catre livrari esueaza cu `UntrustedRoot`, se reincearca de 3 ori si plata raspunde 502.

Din radacina solutiei:

```bash
./run.sh      # optiunea 7: porneste baza, API-ul de livrari in fundal, apoi API-ul cosului
```

sau manual, in doua terminale (intai livrarile):

```bash
docker compose -f Laborator6_PSSC/compose.yaml up -d
dotnet run --project Laborator7_PSSC/Laborator7_PSSC.Delivery
dotnet run --project Laborator7_PSSC/Laborator7_PSSC.Api
```

Exemplu cu `curl`:

```bash
curl -k -X POST https://localhost:7197/carts -H 'Content-Type: application/json' \
  -d '{"customerCode":"C-400","customerName":"Dan Rusu","street":"Str. Mare 3","city":"Iasi","postalCode":"700002","lines":[{"productCode":"MER-0001","quantity":2}]}'
curl -k -X POST https://localhost:7197/carts/<id>/pay
curl -k https://localhost:7297/deliveries/<id>
```

## Teste

```bash
dotnet test Laborator7_PSSC/Laborator7_PSSC.Api.Tests       # politica de reincercare
dotnet test Laborator7_PSSC/Laborator7_PSSC.Domain.Tests    # workflow-urile din Lab 6
```
