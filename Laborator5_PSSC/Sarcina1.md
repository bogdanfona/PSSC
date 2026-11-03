# Sarcina 1 - Analiza solutiei din Exemple

Solutia analizata: [`Lucrarea-05/Exemple/Examples.slnx`](https://github.com/diercan/Laborator-PSSC/tree/main/Lucrarea-05/Exemple) - workflow-ul de publicare a notelor unui examen, cu persistenta in SQL Server prin EF Core 10. Deciziile sunt descrise in ADR-0003 din acelasi laborator.

## Rulare

- Baza `Student` a fost creata cu `Lucrarea-05/SQL/create-db.sql` in containerul SQL Server din Laboratorul 4 (acelasi server, langa baza `ShoppingCart`). `compose.yaml`-ul din exemplu nu a fost folosit, pentru ca porneste tot un SQL Server pe portul 1433.
- Connection string-ul a fost dat prin variabila de mediu `ConnectionStrings__DefaultConnection` (autentificare `sa`, in loc de autentificarea integrata din `appsettings.json`, care nu merge pe macOS).
- Build reusit, **40/40 teste trec**.

Scenarii rulate in consola:

| Input | Rezultat |
|---|---|
| `LM12345` 7.75 / 8, `LM54321` 4 / 7 | CSV publicat; nota finala `7.88` salvata exact (`DECIMAL(4,2)`); `LM54321` fara nota finala (o nota sub 5) -> `NULL` in baza |
| `LM12345` 9.5 / 10 | Upsert: acelasi rand din `Grade` (acelasi `GradeId`) este actualizat, nu se insereaza altul |
| `LM00000` abc / 11, `LM1` 5 / 5 | Publicare esuata cu **toate** erorile: student inexistent, nota examen invalida, nota activitate peste 10, matricol cu format gresit; nimic scris in baza |

## Elemente noi

### Organizarea solutiei

- **Proiecte separate pe straturi**: `Examples.Functional` (Result, Option), `Examples.Domain`, `Examples.Data` (EF Core), `Examples.ConsoleApp` (gazda), `Examples.Domain.Tests`. Domeniul nu are referinta la EF Core, deci regula este impusa de compilator, nu doar de foldere.
- **`.slnx`**: noul format XML de solutie, cu foldere virtuale `/Domain/`, `/Data/`, `/Hosts/`.
- **`Directory.Build.props`**: configuratie comuna pentru toate proiectele (warnings ca erori, analizoare `latest-recommended`, stil de cod verificat la build, `UserSecretsId` comun).
- **`Directory.Packages.props`** (Central Package Management): versiunile pachetelor sunt intr-un singur loc; in `.csproj` nu se mai scrie `Version`.
- **`global.json`** fixeaza SDK-ul; `Directory.Build.targets` da un mesaj clar daca SDK-ul este mai vechi de 10.0.

### Persistenta

- **Porturi in domeniu, adaptoare in `Examples.Data`**: `IStudentsRepository` / `IGradesRepository` sunt implementate de `StudentsRepository` / `GradesRepository`.
- **Workflow-ul primeste repository-urile** (nu functii ca in Lab 3). `ExecuteAsync` este `GetExistingAsync` (impur) -> `Publish(...)` (pur, metoda statica testabila separat) -> `TapAsync(SaveAsync)` (impur) -> `Map(ToEvent)`.
- **Domeniul nu stie de baza de date**: fara `GradeId` sau indicatori de tip "modificat" in starile domeniului.
- **Upsert dupa cheia naturala** (numarul matricol): o singura interogare cu `Include`, apoi actualizare sau inserare, si un singur `SaveChangesAsync`.
- **Model de citire separat**: `IGradesQuery` -> `StudentGradeRow` cu note `decimal?`, pentru ca baza permite `NULL`, iar domeniul nu poate reprezenta note lipsa.
- **Entitati EF separate de tipurile domeniului**: `StudentEntity`, `GradeEntity`.
- **`OnModelCreating` oglindeste exact scriptul SQL**: `HasPrecision(4, 2)`, numele indexurilor si constrangerilor, relatie 1-la-1.
- **Datele din baza proprie sunt de incredere**: un matricol invalid gasit in baza arunca `InvalidDataException` (corupere de date), nu devine eroare de validare.
- **Script SQL idempotent** cu date initiale (`MERGE`), deci exemplul reuseste de la prima rulare.

### Gazda si infrastructura

- **`Host.CreateApplicationBuilder` cu dependency injection**: `AddGradesData(...)` inregistreaza `DbContext`-ul, repository-urile si query-ul ca *scoped*; un `AsyncServiceScope` per rulare. `DbContext`-ul nu mai este creat manual.
- **Configuratie** din `appsettings.json` / user-secrets, cu mesaj clar daca lipseste connection string-ul.
- **`TimeProvider`** injectat in loc de `DateTime.Now`; testele folosesc un `FixedTimeProvider`.
- **`CancellationToken`** transmis prin toate metodele asincrone.
- **Fake-uri pentru porturi in teste**: `FakeStudentsRepository`, `RecordingGradesRepository`.

### Stilul codului (C# 14, stil functional)

- **Extension members** (`extension(Exam.Validated exam) { ... }`): operatiile apar ca metode pe stari (`.Validate()`, `.Calculate()`, `.Publish()`).
- **Stari ca tip-suma inchis**: `Exam.Unvalidated -> Validated -> Calculated -> Published`, cu `Match` exhaustiv.
- **`Option<T>`** pentru nota finala care poate lipsi, in loc de `null`.
- **Validare aplicativa** (`Traverse`, `Result.Combine`): se colecteaza toate erorile, nu doar prima.
- **Erori tipizate** (`ValidationError`, `PublishExamError`), nu siruri de caractere.
- Cuvantul cheie **`field`** in proprietati, **`[GeneratedRegex]`**, comentarii XML `///` pe tipurile publice, **xUnit v3** pe Microsoft Testing Platform.

## Comparatie cu solutia din Laboratorul 5

| Element | Exemple | Laborator5_PSSC |
|---|---|---|
| Porturi in domeniu + adaptoare EF | da | da (`IProductsRepository`, `IOrdersRepository`) |
| I/O doar la margini (citire -> pur -> scriere) | `ExecuteAsync` + `Publish` pura | `ExecuteAsync` + `Place` pura |
| Upsert dupa cheia naturala | matricol | codul clientului |
| Model de citire separat | `IGradesQuery` | `IShopQueries` |
| Proiecte separate pe straturi | da | da (`Functional`, `Domain`, `Data`, `ConsoleApp`, `Domain.Tests`) |
| Dependency injection / `Host` | da | da (`AddShoppingCartData`, un scope per actiune) |
| `CancellationToken`, `TimeProvider` | da | da (plus Ctrl+C pentru anulare) |
| Central Package Management, `Directory.Build.props` | da | da (doar pentru Lab 5) |
| Tranzactie si verificare atomica a stocului la salvare | nu (fereastra netranzactionala acceptata in ADR) | da |
