# Laborator 8 - Comunicare asincrona intre contexte folosind evenimente

Cand un cos este platit, API-ul cosului publica evenimentul **„comanda preluata”** (`pssc.orders.placed.v1`) pe topicul Azure Service Bus `order-placed`, in format CloudEvents. Doua servicii worker il primesc independent, fiecare pe subscriptia lui:

- **facturarea** (`invoicing`) ruleaza workflow-ul de generare a facturii si o salveaza in baza;
- **livrarea** (`shipping`) initiaza livrarea apeland API-ul de livrari din Laboratorul 7.

Totul ruleaza local, cu emulatorul Azure Service Bus in Docker, fara un namespace Azure real.

Analiza solutiei din `Exemple` (Sarcina 1) este in [Sarcina1.md](Sarcina1.md).

## Fluxul (Sarcina 3)

```
POST /carts/{id}/pay ──> Laborator8_PSSC.Api ──> SQL Server (cosul devine Paid)
                                │
                                └─ OrderPlacedIntegrationEvent (CloudEvent) ──> topic order-placed
                                                                                 │
                       ┌─────────────────────────────────────────────────────────┴───────────────────────┐
                 subscriptia invoicing                                                         subscriptia shipping
                       │                                                                               │
        Invoicing.EventProcessor                                                         Shipping.EventProcessor
        GenerateInvoiceWorkflow:                                                         DeliveryApiClient (3 reincercari HTTP)
        validare -> TVA -> numar factura                                                         │
                       │                                                                 Laborator8_PSSC.Delivery
        SQL Server, schema invoicing (Invoices, InvoiceLines)                            (AWB, data estimata)
```

API-ul raspunde imediat dupa plata; nu asteapta nici facturarea, nici livrarea. Daca un worker este oprit, mesajul il asteapta in subscriptia lui.

### Workflow-ul de facturare (implementat complet)

In `Laborator8_PSSC.Invoicing.Domain`, in acelasi stil ca workflow-urile din Lab 3-7 (stari ca tipuri, operatii pure, porturi):

```
UnvalidatedInvoice ──Validate──> ValidatedInvoice ──Calculate──> CalculatedInvoice ──Issue──> IssuedInvoice
```

| Pas | Ce face |
|---|---|
| `ValidateInvoiceOperation` | client, adresa, cel putin un produs, cantitati pozitive, preturi nenegative; raporteaza toate problemele deodata |
| `CalculateInvoiceOperation` | preturile magazinului includ TVA (21%): fiecare linie se imparte in net + TVA (TVA = total - net, ca sa nu se piarda niciun ban la rotunjire); verifica ca totalul facturii este egal cu ce a platit clientul |
| `IssueInvoiceOperation` | numarul facturii, derivat din comanda: `FCT-2026-003003` |
| `GenerateInvoiceWorkflow.ExecuteAsync` | citire (exista deja factura?) -> `Generate` (pur) -> salvare |

Exemplu real din rulare: comanda 3003 (3 × 4,20 + 2 × 6,50) -> factura `FCT-2026-003003`, net 21,15, TVA 4,45, total 25,60 RON.

### Initierea livrarii

`Shipping.EventProcessor` trimite comanda la `POST /deliveries` prin clientul tipizat din Lab 7 (3 reincercari exponentiale). Daca API-ul de livrari tot nu raspunde, handler-ul cere relivrarea mesajului.

## Coada si topicul (Sarcina 2)

`infra/servicebus/Config.json` defineste coada `orders` si topicul `order-placed` cu subscriptiile `invoicing` si `shipping`. `Laborator8_PSSC.MessagingDemo` este emitatorul si receptorul, scrise direct cu SDK-ul Service Bus:

```bash
cd Laborator8_PSSC/Laborator8_PSSC.MessagingDemo
dotnet run -- queue-receive                 # terminal 1
dotnet run -- queue-receive                 # terminal 2
dotnet run -- queue-send 4                  # terminal 3: fiecare mesaj ajunge la UN singur receptor (1-la-1)

dotnet run -- topic-receive invoicing       # terminal 1
dotnet run -- topic-receive shipping        # terminal 2
dotnet run -- topic-send 2                  # terminal 3: fiecare subscriptie primeste FIECARE mesaj (1-la-mai-multi)

dotnet run -- dead-letters shipping         # mesajele moarte dintr-o subscriptie, cu motivul
```

Rezultat la rulare: pe coada, cele 4 mesaje s-au impartit 2 + 2 intre receptori; pe topic, ambele subscriptii au primit ambele mesaje.

Demo-ul de topic foloseste acelasi topic ca Sarcina 3. Daca worker-ii ruleaza in acelasi timp, ei primesc si mesajele demo, care nu sunt CloudEvents, si le muta in dead-letter (`InvalidCloudEvent`) - un exemplu direct al politicii de mesaje moarte.

## Concepte, in cod

| Concept | Unde |
|---|---|
| **Rutare dupa tipul evenimentului** | `OrderPlacedIntegrationEvent.EventType = "pssc.orders.placed.v1"` (membru `static abstract`); ascultatorul alege handler-ul dupa `CloudEvent.Type`, nu dupa numele clasei |
| **CloudEvents** | `ServiceBusTopicEventSender`: plic structurat JSON (`id`, `type`, `source`, `time`, `subject`, `data`) |
| **Decontare explicita** | `ServiceBusTopicEventListener.SettleAsync`: `Completed` -> complete; `Retry` -> abandon dupa o pauza; `Failed` -> dead-letter. Mesajele nedecodabile sau fara handler -> dead-letter |
| **At-least-once + idempotenta** | facturare: o factura per comanda (verificare inainte + index unic `OrderId` in baza, pentru doua livrari simultane); livrare: API-ul de livrari pastreaza o livrare per comanda |
| **Mesaje moarte** | date invalide sau total gresit -> `Failed`; API de livrari care respinge cererea (400) -> `Failed`; dupa `MaxDeliveryCount` (5) livrari esuate -> broker-ul muta mesajul singur |
| **Relivrare cu pauza** | la `Retry`, mesajul este abandonat dupa 2, 4, 8, 16 s (maxim 30 s, sub `LockDuration` de 1 min), deci cele 5 livrari acopera aproape un minut. Fara pauza (ca in exemplul cursului) se consumau in ~12 s |
| **Worker** | `Worker : BackgroundService` porneste ascultatorul si il opreste elegant la Ctrl+C |
| **Fara outbox** | plata si publicarea nu sunt o tranzactie. Daca Service Bus nu raspunde, plata ramane salvata, API-ul raspunde **503** si evenimentul se poate republica cu `POST /carts/{id}/order-placed` (sigur, pentru ca handler-ele sunt idempotente). Daca procesul se opreste exact intre salvare si publicare, evenimentul se pierde; un outbox tranzactional ar rezolva asta |

## Scenarii verificate la rulare

| Scenariu | Rezultat |
|---|---|
| Plata unui cos | 200 imediat; factura emisa; livrarea creata dupa 2 raspunsuri 503 simulate (reincercari HTTP) |
| Acelasi eveniment publicat din nou | facturare: `already has invoice ... repeated delivery ignored`; livrare: aceeasi livrare (`new: False`) |
| Mesaj care nu e CloudEvent pe topic | dead-letter `InvalidCloudEvent` in ambele subscriptii |
| API-ul de livrari oprit la plata, pornit dupa 20 s | `Retry` la livrarile 1-3, `Completed` la livrarea 4; facturarea nu a fost afectata |
| Emulatorul oprit la plata | 503 in ~5 s; dupa repornire, `POST /carts/{id}/order-placed` -> factura si livrarea create |

## Structura

| Proiect | Rol |
|---|---|
| `Laborator8_PSSC.Events` | **Nou.** Abstractii de mesagerie fara dependinte: `IIntegrationEvent`, `IEventSender`, `IEventHandler<T>`, `IEventListener`, `EventProcessingResult`, `TopicName`, `SubscriptionName` |
| `Laborator8_PSSC.Events.ServiceBus` | **Nou.** Implementarea peste Azure Service Bus cu CloudEvents: trimitere, ascultare, decontare |
| `Laborator8_PSSC.Contracts` | DTO-urile de livrare (Lab 7) + **evenimentul** `OrderPlacedIntegrationEvent` |
| `Laborator8_PSSC.Api` | API-ul cosului; la plata publica evenimentul (nu mai apeleaza livrarile direct) |
| `Laborator8_PSSC.Invoicing.Domain` | **Nou.** Workflow-ul de facturare, pur, cu portul `IInvoicesRepository` |
| `Laborator8_PSSC.Invoicing.EventProcessor` | **Nou.** Worker-ul de facturare: handler, `InvoicingContext` + `InvoicesRepository` (EF Core) |
| `Laborator8_PSSC.Shipping.EventProcessor` | **Nou.** Worker-ul de livrare: handler + clientul HTTP rezilient (mutat din API-ul Lab 7) |
| `Laborator8_PSSC.Delivery` | API-ul de livrari din Lab 7 |
| `Laborator8_PSSC.MessagingDemo` | **Nou.** Sarcina 2: emitator/receptor pe coada si pe topic |
| `Laborator8_PSSC.EventProcessors.Tests` | **Nou.** Teste pentru facturare, handler-ul de livrare, reincercarile HTTP, dispatcher si pauza de relivrare |
| `Functional`, `Domain`, `Data`, `Domain.Tests` | Din Lab 7, neschimbate |
| `infra/servicebus/Config.json` | Coada `orders`, topicul `order-placed`, subscriptiile `invoicing` si `shipping` |
| `SQL/update-db.sql` | Schema `invoicing` cu tabelele `Invoices` si `InvoiceLines` |
| `compose.yaml` | SQL Server-ul din Lab 4-7 + scriptul de mai sus + emulatorul Service Bus |

## Pornire

Necesita configurarea din Laboratorul 4 (Docker Desktop, `Laborator4_PSSC/.env`, connection string-ul bazei in user-secrets). Connection string-ul emulatorului este o valoare publica si sta in `appsettings.Development.json`:

```
Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;
```

Un connection string catre un namespace Azure real nu se pune niciodata in `appsettings.json`: se da prin `dotnet user-secrets` sau prin variabila de mediu `ConnectionStrings__ServiceBus`.

Worker-ul de livrare apeleaza API-ul de livrari prin HTTPS, deci certificatul de dezvoltare trebuie sa fie de incredere (o singura data): `dotnet dev-certs https --trust`.

Din radacina solutiei:

```bash
./run.sh      # optiunea 8: baza, emulatorul, API-ul de livrari si worker-ii in fundal, apoi API-ul cosului
```

sau manual (fiecare in terminalul lui):

```bash
docker compose -f Laborator8_PSSC/compose.yaml up -d
dotnet run --project Laborator8_PSSC/Laborator8_PSSC.Delivery
dotnet run --project Laborator8_PSSC/Laborator8_PSSC.Invoicing.EventProcessor
dotnet run --project Laborator8_PSSC/Laborator8_PSSC.Shipping.EventProcessor
dotnet run --project Laborator8_PSSC/Laborator8_PSSC.Api          # https://localhost:7198/swagger
```

Emulatorul are nevoie de aproximativ un minut ca sa porneasca (`curl http://localhost:5300/health` -> `healthy`) si nu pastreaza mesajele intre reporniri.

Facturile se pot vedea in baza:

```sql
SELECT i.Number, i.OrderId, i.NetAmount, i.VatAmount, i.Total FROM ShoppingCart.invoicing.Invoices i;
```

## Teste

```bash
dotnet test Laborator8_PSSC/Laborator8_PSSC.EventProcessors.Tests   # facturare, livrare, mesagerie (fara broker)
dotnet test Laborator8_PSSC/Laborator8_PSSC.Domain.Tests            # workflow-urile cosului din Lab 6-7
```
