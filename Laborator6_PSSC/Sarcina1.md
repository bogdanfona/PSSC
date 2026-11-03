# Sarcina 1 - Analiza solutiei din Exemple

Solutia analizata: [`Lucrarea-06/Exemple/Examples.slnx`](https://github.com/diercan/Laborator-PSSC/tree/main/Lucrarea-06/Exemple) - workflow-ul de publicare a notelor din Lucrarea 5, expus printr-un API Web. Deciziile sunt descrise in ADR-0004.

## Rulare

- Baza `Student` din Lucrarea 5, in containerul SQL Server din Laboratorul 4; connection string-ul dat prin `ConnectionStrings__DefaultConnection`.
- `dotnet run --project Examples.Api` -> `https://localhost:7195/swagger`.
- Build reusit, **40/40 teste trec**.

| Cerere | Raspuns |
|---|---|
| `POST /grades` cu `LM67890` 8.5 / 9 | 200 `{ csv, publishedAt }`, nota finala 8.75 |
| `POST /grades` cu matricol inexistent, nota 11, nota lipsa | 400 `ValidationProblemDetails`, erorile grupate dupa tip (`StudentNotFound`, `InvalidExamGrade`, `InvalidActivityGrade`), cu `traceId` |
| `GET /grades` | 200, catalogul cu note `null` unde lipsesc (modelul de citire) |
| `/openapi/v1.json` | `POST /grades` -> 200, 400; `GET /grades` -> 200 |

Observatii:

- Un JSON invalid in corpul cererii da **500**, nu 400, cand API-ul ruleaza cu profilul `https` (mediul Development): acolo ASP.NET Core arunca `BadHttpRequestException` pentru cererile malformate, iar `UseExceptionHandler` o transforma in 500. In Production raspunsul este 400.
- Un matricol duplicat este raportat doar cand toate notele sunt valide, pentru ca verificarea duplicatelor (`RejectDuplicates`) ruleaza cu `Bind` dupa validarea fiecarei note.

## Ce s-a schimbat fata de Lucrarea 5

Doar gazda: `Examples.ConsoleApp` este inlocuit cu `Examples.Api`. Proiectele `Functional`, `Domain`, `Data` si testele sunt identice - dovada ca workflow-ul nu depinde de felul in care este apelat (consola sau HTTP).

## Elemente noi

### API minimal

- **Fara controllere**: `Program.cs` cu instructiuni de nivel superior si `WebApplication.CreateBuilder`; endpoint-urile sunt functii statice inregistrate cu `MapPost` / `MapGet`.
- **Grup de rute** (`MapGroup("/grades").WithTags("Grades")`) in loc de clasa controller; `WithName` / `WithSummary` pentru document.
- **Endpoint-uri ca metode de extensie** (`app.MapGrades()`), intr-un fisier separat (`Endpoints/GradesEndpoints.cs`).
- **Injectare prin parametri**: functia endpoint-ului primeste direct corpul cererii (`IReadOnlyList<InputGrade>`), serviciile (`PublishExamWorkflow`, `IGradesQuery`) si `CancellationToken`-ul cererii.

### Raspunsuri tipizate

- **`TypedResults`** in loc de `Results` / `IActionResult`.
- **Tipul de retur enumera raspunsurile**: `Results<Ok<PublishGradesResponse>, ValidationProblem>`. Compilatorul verifica fiecare ramura, iar documentul OpenAPI se genereaza din tip, fara atribute `[ProducesResponseType]`.
- **`Result` -> HTTP intr-un singur loc**: `result.Match(published => TypedResults.Ok(...), error => error.ToValidationProblem())`.
- **Raspuns de succes ca record** (`PublishGradesResponse`), nu tip anonim, deci schema OpenAPI este completa.

### Erori standard (RFC 9457)

- **`AddProblemDetails`** cu `traceId` adaugat in fiecare raspuns de eroare.
- **400 `ValidationProblem`**: `ToValidationProblem()` grupeaza `ValidationError` dupa `Code` (numele tipului) si pune mesajele ca valori.
- **`UseExceptionHandler` + `UseStatusCodePages`**: exceptiile de infrastructura nu se prind in endpoint; devin 500 `application/problem+json`.

### Granita: DTO-uri si mapare

- **DTO de intrare fara DataAnnotations** (`InputGrade(string? RegistrationNumber, decimal? Exam, decimal? Activity)`): toate campurile optionale; regulile raman doar in domeniu, ca sa nu existe doua seturi care sa diverga.
- **Maparea DTO -> domeniu ca bloc de extensie C# 14** (`InputGradeMapping.ToUnvalidated()`), cu `CultureInfo.InvariantCulture` pentru numere.
- **`RespectNullableAnnotations = true`** pentru serializarea JSON.

### OpenAPI si configurare

- **Document OpenAPI nativ** (`Microsoft.AspNetCore.OpenApi`, `AddOpenApi` / `MapOpenApi`) in loc de generatorul Swashbuckle; `Swashbuckle.AspNetCore.SwaggerUI` doar serveste interfata.
- OpenAPI si Swagger UI doar in mediul **Development**.
- **`launchSettings.json`** cu un singur profil `https` si `launchUrl: swagger`; `appsettings.Development.json` separat.
- **Proiect `Microsoft.NET.Sdk.Web`**, nu `Microsoft.NET.Sdk`.
- **Fara `UseAuthorization`** (nu exista autentificare) si fara `AddControllers`.

## Ce am preluat in Laboratorul 6

| Element din exemplu | Laborator6_PSSC |
|---|---|
| API minimal, `MapGroup`, endpoint-uri ca extensii | `CartsEndpoints.MapCarts()`, `ProductsEndpoints.MapProducts()` |
| `Results<...>` tipizate | `Results<Ok<CartView>, ValidationProblem, ProblemHttpResult>` etc. |
| Maparea erorilor intr-un singur loc | `ErrorMapping.ToHttp()`; in plus 404 si 409, nu doar 400 |
| DTO-uri fara reguli, mapare cu cultura invarianta | `PlaceOrderRequest`, `AddCartItemRequest`, `RequestMapping` |
| ProblemDetails cu `traceId`, `UseExceptionHandler` | da; in plus `StatusCodeSelector` pastreaza 400 pentru JSON invalid si in Development |
| OpenAPI nativ + Swagger UI | da, `https://localhost:7196/swagger` |
| Vizualizare prin modelul de citire | `GET /carts/{id}` -> `CartView` |
