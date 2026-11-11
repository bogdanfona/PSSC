# Sarcina 1 - Analiza solutiei din Exemple

Solutia analizata: [`Lucrarea-08/Exemple/Examples.slnx`](https://github.com/diercan/Laborator-PSSC/tree/main/Lucrarea-08/Exemple) - workflow-ul de publicare a notelor, care dupa salvare publica un eveniment pe Azure Service Bus in loc sa apeleze sincron un alt API (ca in Lucrarea 7). Deciziile sunt descrise in ADR-0006.

## Rulare

Dupa enunt:

```bash
cp ../.env.example ../.env
docker compose -f ../compose.yaml up -d          # SQL Server, scriptul bazei Student, emulatorul Service Bus
dotnet run --project Exemple/Examples.Api
dotnet run --project Exemple/Examples.Accommodation.EventProcessor
```

Ce trebuie urmarit:

| Scenariu | Rezultat asteptat |
|---|---|
| `POST /grades` cu note valide | 200; in consola Api: `Event pssc.grades.published.v1 sent to topic grades`; in consola worker-ului: `Received N grades published at ...` si `settled as Completed` |
| `POST /grades` cu note invalide | 400 `ValidationProblem`; niciun eveniment trimis |
| Worker-ul oprit in timpul unui `POST /grades` | API-ul raspunde tot 200 (nu mai depinde de consumator); mesajul asteapta in subscriptia `accommodation` si este procesat cand worker-ul porneste |
| Emulatorul oprit | `POST /grades` da 500: notele sunt salvate, evenimentul nu (fara outbox) |

Comportamentul de mesagerie (topic, subscriptii, CloudEvents, dead-letter, relivrare) l-am verificat rulandu-l in solutia mea din Laboratorul 8, care foloseste aceleasi abstractii si acelasi emulator (vezi README).

## Ce s-a schimbat fata de Lucrarea 7

| Lucrarea 7 | Lucrarea 8 |
|---|---|
| `ReportApiClient` (HTTP) apelat in endpoint | `IEventSender.SendAsync(topic, eveniment)` |
| `Examples.ReportGenerator` (API, trebuie sa fie pornit) | `Examples.Accommodation.EventProcessor` (worker, poate porni oricand) |
| Esecul consumatorului -> 500 pentru client | Clientul nu afla nimic de consumator; esecul se trateaza prin relivrare / dead-letter |
| Contract HTTP (`ExamPublishedReport`) | Eveniment de integrare (`GradesPublishedEvent : IIntegrationEvent`) |
| Microsoft.Extensions.Http.Resilience | Azure.Messaging.ServiceBus + CloudNative.CloudEvents |

Proiectele `Functional`, `Domain`, `Data` si testele raman aceleasi.

## Elemente noi

### `Examples.Events` - abstractii independente de transport

- Proiect **fara nicio dependinta** (nici Service Bus, nici CloudEvents): codul de business depinde doar de interfete.
- **`IIntegrationEvent` cu `static abstract string EventType`** (C# 11): fiecare eveniment isi declara numele de rutare la compilare, stabil si versionat (`"pssc.grades.published.v1"`). Nu se foloseste `typeof(T).Name`, care s-ar schimba la o redenumire a clasei fara sa anunte consumatorii.
- **`IEventSender.SendAsync<TEvent>(TopicName, TEvent, ct)`** - portul de publicare.
- **`IEventHandler<in TEvent>`** primeste un record simplu, nu un `CloudEvent` sau un `ServiceBusReceivedMessage`, si intoarce **`EventProcessingResult`** (`Completed`, `Retry`, `Failed`). Handler-ul spune *ce* s-a intamplat; ascultatorul decide *cum* se deconteaza mesajul.
- **`IEventListener : IAsyncDisposable`** cu `StartAsync` idempotent si `StopAsync`.
- **`TopicName` si `SubscriptionName`** ca `readonly record struct` cu garda pe sir gol: nu pot fi confundate intre ele sau cu alte siruri.

### `Examples.Events.ServiceBus` - implementarea peste Azure Service Bus

- **CloudEvents in mod structurat** (`JsonEventFormatter`, `application/cloudevents+json`): plicul standard contine `id`, `type`, `source`, `time`, `subject`, `datacontenttype` si `data`. `Source` vine din configuratie (`ServiceBusEvents:Source`), `Time` din `TimeProvider`.
- **`ServiceBusMessage`** primeste `MessageId = CloudEvent.Id`, `ContentType` si `Subject = EventType` (rutare/filtrare posibila fara a citi corpul).
- **Un `ServiceBusSender` per topic**, tinut in cache intr-un `ConcurrentDictionary`.
- **`ServiceBusTopicEventListener`** cu `ServiceBusProcessor` (`AutoCompleteMessages = false`, `MaxConcurrentCalls = 2`) si decontare explicita:

  | Situatie | Decontare |
  |---|---|
  | Mesajul nu este un CloudEvent valid | dead-letter `InvalidCloudEvent` |
  | Niciun handler pentru `type` | dead-letter `NoHandler` |
  | `Completed` | `Complete` |
  | `Retry` | `Abandon` (relivrare) |
  | `Failed` | dead-letter `HandlerFailed` |
  | Exceptie in handler | nu se prinde: procesorul abandoneaza, broker-ul muta mesajul in dead-letter dupa `MaxDeliveryCount` |

- **Rute intr-un `FrozenDictionary`** dupa `EventType`: doi handleri pentru acelasi tip arunca la pornire, nu la primul mesaj.
- **`EventDispatcher<TEvent>`** (intern) deserializeaza `data` cu `JsonSerializerOptions.Web`; `null` inseamna date corupte -> `Failed`. Handler-ul este rezolvat **scoped, per mesaj** (`IServiceScopeFactory`), deci poate folosi un `DbContext`.
- **Inregistrare fluenta**: `AddServiceBusEventSender(configuration)` si `AddServiceBusEventListener().AddHandler<TEvent, THandler>()`; `ServiceBusClient` vine din `AddAzureClients` (`Microsoft.Extensions.Azure`).
- **`[LoggerMessage]`** pentru toate mesajele de log.

### `Examples.Accommodation.EventProcessor` - worker-ul

- **Gazda generica** (`Host.CreateApplicationBuilder`), proiect `Microsoft.NET.Sdk` cu `OutputType Exe`; `appsettings*.json` copiate explicit in output (SDK-ul simplu nu o face singur).
- **`Worker : BackgroundService`**: porneste ascultatorul, apoi doar asteapta oprirea (ascultatorul este bazat pe push); `StopAsync` opreste procesorul elegant.
- **`EventProcessorOptions`** (`TopicName`, `SubscriptionName`) cu `[Required]` si `ValidateOnStart`, din sectiunea `EventProcessor`.
- **`GradesPublishedEventHandler`**: exemplu minimal, logheaza si intoarce `Completed`.

### Contractul

- **`GradesPublishedEvent`** in `Examples.Contracts` (care acum referentiaza `Examples.Events`), separat de evenimentul de domeniu `ExamPublishedEvent`; maparea `ToIntegrationEvent()` despacheteaza obiectele-valoare. `IReadOnlyList<T>`, nu `List<T>`.

### Infrastructura si secrete

- **Emulatorul Azure Service Bus** in Docker (`compose.yaml` la radacina depozitului), configurat prin `infra/servicebus/Config.json` (topic `grades`, subscriptie `accommodation`, `MaxDeliveryCount 5`, `LockDuration PT1M`, TTL 1 h, dead-letter la expirare). Isi tine metadatele in acelasi SQL Server.
- **Connection string-ul emulatorului** este o constanta publica si sta doar in `appsettings.Development.json`; un connection string real vine doar din `dotnet user-secrets` sau `ConnectionStrings__ServiceBus`.

### Limitari asumate (ADR-0006)

- **Fara outbox**: salvarea in baza si trimiterea evenimentului nu sunt o tranzactie; daca procesul se opreste intre ele, evenimentul se pierde.
- **At-least-once**: acelasi mesaj poate ajunge de mai multe ori, deci handler-ele trebuie sa fie idempotente.

## Ce am preluat in Laboratorul 8

| Element din exemplu | Laborator8_PSSC |
|---|---|
| `Examples.Events` | `Laborator8_PSSC.Events` (aceleasi interfete) |
| `Examples.Events.ServiceBus` | `Laborator8_PSSC.Events.ServiceBus`; in plus, la `Retry` mesajul este abandonat dupa o pauza exponentiala (2, 4, 8, 16 s), nu imediat - altfel cele 5 livrari se consumau in cateva secunde (am observat asta la rulare) |
| `GradesPublishedEvent` | `OrderPlacedIntegrationEvent`, `EventType = "pssc.orders.placed.v1"` |
| Un worker | doi worker-i pe acelasi topic: `Invoicing.EventProcessor` si `Shipping.EventProcessor` |
| Handler care doar logheaza | facturare: workflow DDD complet cu salvare in baza; livrare: apel la API-ul de livrari din Lab 7 |
| 500 cand emulatorul e oprit | 503 cu mesaj clar si `POST /carts/{id}/order-placed` pentru republicare |
