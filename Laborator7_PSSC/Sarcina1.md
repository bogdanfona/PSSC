# Sarcina 1 - Analiza solutiei din Exemple

Solutia analizata: [`Lucrarea-07/Exemple/Examples.slnx`](https://github.com/diercan/Laborator-PSSC/tree/main/Lucrarea-07/Exemple) - workflow-ul de publicare a notelor din Lucrarea 6, care dupa publicare apeleaza un al doilea API, `Examples.ReportGenerator`. Deciziile sunt descrise in ADR-0005.

## Rulare

Ordinea din enunt: intai `Examples.ReportGenerator` (`https://localhost:7286`), apoi `Examples.Api` (`https://localhost:7195/swagger`). Baza `Student` este cea din Lucrarea 5, in containerul SQL Server din Laboratorul 4; connection string-ul se da prin `ConnectionStrings__DefaultConnection`.

```bash
dotnet run --project Exemple/Examples.ReportGenerator
ConnectionStrings__DefaultConnection="Server=localhost,1433;Database=Student;User Id=sa;Password=<parola>;TrustServerCertificate=true" \
  dotnet run --project Exemple/Examples.Api
```

Ce trebuie urmarit:

| Scenariu | Rezultat asteptat |
|---|---|
| `POST /grades` cu note valide, ReportGenerator pornit | 200; in consola ReportGenerator apar doua linii de log (raport semestrial, burse) |
| `POST /grades` cu note invalide | 400 `ValidationProblem`; ReportGenerator nu este apelat |
| `POST /grades` cu ReportGenerator oprit | in consola Api apar 3 evenimente `OnRetry` (Polly), la aproximativ 0,6 s, 1,2 s, 2,4 s, apoi **500**; notele sunt totusi salvate in baza |

Observatie: Api apeleaza ReportGenerator prin HTTPS cu certificatul de dezvoltare. Daca certificatul nu este de incredere pe masina (`dotnet dev-certs https --check --trust`), fiecare apel esueaza cu `AuthenticationException: UntrustedRoot`, se reincearca de 3 ori si se termina cu 500. Se rezolva o data cu `dotnet dev-certs https --trust`. Am intalnit exact aceasta situatie la rularea solutiei din Laboratorul 7.

## Ce s-a schimbat fata de Lucrarea 6

Proiectele `Functional`, `Domain`, `Data` si testele sunt identice. Diferentele sunt doar la gazda si la cele doua proiecte noi:

| Fisier | Schimbare |
|---|---|
| `Examples.Contracts/` | proiect nou: `ExamPublishedReport`, `ReportAcknowledgement` |
| `Examples.ReportGenerator/` | proiect nou: al doilea API |
| `Examples.Api/Clients/` | nou: `ReportApiClient`, `ReportApiOptions`, `AddReportApiClient` |
| `Examples.Api/Mapping/ExamPublishedEventMapping.cs` | nou: eveniment de domeniu -> contract |
| `Examples.Api/Endpoints/GradesEndpoints.cs` | pe succes apeleaza cele doua endpoint-uri de raport inainte de 200 |
| `Examples.Api/Program.cs` | o singura linie noua: `AddReportApiClient(builder.Configuration)` |
| `Examples.Api/appsettings.json` | sectiunea `ReportApi` (adresa, reincercari) |
| `Examples.slnx`, `Examples.slnLaunch.user` | cele doua proiecte noi; profil care porneste ambele API-uri |

## Elemente noi

### Doua contexte, doua procese

- **Al doilea API ca proces separat** (`Examples.ReportGenerator`, alt port): contextul de raportare nu impartaseste cod de domeniu sau baza de date cu cel de note; comunica doar prin HTTP.
- **API-ul apelat este intentionat minimal**: doua `POST` care doar logheaza si confirma cu `ReportAcknowledgement`.
- **`[LoggerMessage]`** (`static partial`) pentru logare generata la compilare, fara alocari; se logheaza doar data publicarii, nu continutul CSV.

### Contracte partajate

- **Proiect `Examples.Contracts`** fara referinte si fara logica, doar `record`-uri serializabile, referentiat de ambele API-uri.
- **De ce nu se referentiaza proiectul web al celuilalt API**: ASP.NET Core ar incarca endpoint-urile lui ca *application parts* si in apelant; versiunea veche a exemplului le ascundea din OpenAPI cu un filtru.
- **Maparea eveniment de domeniu -> contract la granita** (`published.ToReport()`), deci domeniul nu depinde de contract si contractul nu depinde de domeniu.

### Client HTTP tipizat

- **`ReportApiClient(HttpClient http)`** cu constructor primar; o metoda pe operatie, nu un `HttpClient` folosit direct in endpoint.
- **`AddHttpClient<ReportApiClient>`**: `IHttpClientFactory` gestioneaza conexiunile (fara epuizarea socket-urilor), clientul se injecteaza in endpoint ca orice serviciu.
- **`PostAsJsonAsync` / `ReadFromJsonAsync`**, `EnsureSuccessStatusCode`, `using` pe `HttpResponseMessage`, `CancellationToken` transmis.

### Rezilienta

- **`Microsoft.Extensions.Http.Resilience`** (Polly v8) in loc de vechiul `Microsoft.Extensions.Http.Polly` cu `AddPolicyHandler`.
- **`AddResilienceHandler("report-api", ...)`** cu un pipeline construit explicit:
  - `AddRetry(HttpRetryStrategyOptions)`: 3 reincercari, `DelayBackoffType.Exponential`, `UseJitter = true`, intarziere de baza 600 ms. Erorile tranzitorii sunt cele implicite: 5xx, 408, 429, `HttpRequestException`, expirarea unei incercari.
  - `AddTimeout(5 s)` adaugat **dupa** retry, deci limiteaza fiecare incercare, nu tot apelul.
- **Polly logheaza singur** fiecare incercare (`Execution attempt`) si fiecare reincercare (`OnRetry`), deci politica se vede in consola fara cod in plus.

### Configurare validata

- **Options pattern**: `ReportApiOptions` legat din sectiunea `ReportApi`, cu `[Required]` si `[Range]`, plus `ValidateDataAnnotations().ValidateOnStart()`: o adresa lipsa sau un numar de reincercari absurd opresc aplicatia la pornire, nu la primul apel.
- **Adresa celuilalt API nu este hard-codata**: vine din `appsettings.json` (sau din variabila de mediu `ReportApi__BaseAddress`).

### Apeluri concurente si propagarea esecului

- **`Task.WhenAll`** pentru cele doua apeluri independente: durata este a celui mai lent, nu suma.
- **Excepțiile nu se prind in endpoint**: dupa epuizarea reincercarilor ajung la `UseExceptionHandler` si devin 500 `ProblemDetails`. ADR-ul recunoaste compromisul: notele sunt deja salvate, deci clientul primeste 500 desi publicarea a reusit partial - motivatia pentru comunicarea asincrona din Lucrarea 8.
- **Reincercarea unui `POST`** este acceptata pentru ca endpoint-urile de raport sunt idempotente (doar logheaza); pentru tinte cu efecte secundare ADR-ul indica `DisableForUnsafeHttpMethods()`.

### Unelte

- **`Examples.slnLaunch.user`**: profil de lansare multipla („Api + ReportGenerator”) care porneste generatorul primul; este urmarit in git printr-o exceptie in `.gitignore` (`!*.slnLaunch.user`).

## Ce am preluat in Laboratorul 7

| Element din exemplu | Laborator7_PSSC |
|---|---|
| Proiect de contracte fara logica | `Laborator7_PSSC.Contracts/Deliveries`: `DeliveryRequest`, `DeliveryItem`, `DeliveryConfirmation` |
| Al doilea API, proces separat | `Laborator7_PSSC.Delivery` pe `https://localhost:7297` |
| Client tipizat + `AddResilienceHandler` (retry exponential, jitter, timeout per incercare) | `DeliveryApiClient`, `AddDeliveryApiClient` |
| Optiuni validate la pornire, adresa din configuratie | `DeliveryApiOptions`, sectiunea `DeliveryApi` |
| Apelul dupa succesul workflow-ului | dupa plata (`POST /carts/{id}/pay`) |
| Esecul dependentei nu se mascheaza | diferit: in loc de 500 generic, **502** cu mesaj clar si `POST /carts/{id}/delivery` pentru reprogramare |
| `POST` reincercat pentru ca tinta e idempotenta | API-ul de livrari pastreaza o singura livrare per cos; o cerere repetata intoarce livrarea existenta |
| Reincercarile vizibile | in plus: `Delivery:FailFirstAttempts` face API-ul de livrari sa raspunda 503 la primele incercari, si teste care numara incercarile |
