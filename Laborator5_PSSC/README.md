# Laborator 5 - Interactiunea cu baza de date

Workflow-ul de plasare a comenzii din Laboratorul 3, legat de baza de date `ShoppingCart` din Laboratorul 4 (EF Core 10, abordare SQL-first).

Fluxul unei comenzi, cu I/O doar la margini:

```
PlaceOrderWorkflow.ExecuteAsync:
  citire din baza   ->  logica pura                       ->  scriere in baza
  (GetByCodesAsync)     (PlaceOrderWorkflow.Place, static)     (SaveAsync)
```

1. **Inainte de logica pura** se incarca din baza, prin portul `IProductsRepository`, doar produsele din comanda (cod, nume, pret, stoc) intr-un `ProductCatalog`.
2. **`Place`** (validare -> produse existente -> stoc -> adresa -> pret) este pipeline-ul din Laboratorul 3. Functiile de verificare a existentei, stocului si pretului sunt metodele `ProductCatalog`, deci folosesc date din baza, dar `Place` ramane pura: primeste totul ca parametru, inclusiv ora curenta.
3. **Dupa logica pura**, daca comanda a fost plasata, portul `IOrdersRepository` o salveaza intr-o singura tranzactie.

Analiza solutiei din `Exemple` (Sarcina 1) este in [Sarcina1.md](Sarcina1.md).

## Structura

Cate un proiect pe strat; dependintele merg doar spre interior (`ConsoleApp -> Data -> Domain -> Functional`), deci domeniul nu poate folosi EF Core nici din greseala.

| Proiect / fisier | Rol |
|---|---|
| `Laborator5_PSSC.Functional/` | `Result<T, TError>` cu `Bind` / `Map` |
| `Laborator5_PSSC.Domain/` | Modelul de domeniu din Lab 3: value objects, operatii, erori, evenimente |
| `Laborator5_PSSC.Domain/Models/ProductCatalog.cs` | Starea produselor incarcata din baza inainte de logica pura |
| `Laborator5_PSSC.Domain/Repositories/` | Porturile domeniului: `IProductsRepository`, `IOrdersRepository` |
| `Laborator5_PSSC.Domain/Workflows/PlaceOrderWorkflow.cs` | `ExecuteAsync` (citire -> `Place` -> scriere) si `Place` (pura) |
| `Laborator5_PSSC.Data/` | `ShoppingCartContext`, entitatile din Lab 4 (plus `Stock`), adaptoarele EF Core |
| `Laborator5_PSSC.Data/Queries/` | Modelul de citire separat (`IShopQueries`) pentru afisarea catalogului si a comenzilor |
| `Laborator5_PSSC.Data/ServiceCollectionExtensions.cs` | `AddShoppingCartData`: inregistreaza contextul si adaptoarele in DI |
| `Laborator5_PSSC.ConsoleApp/` | Gazda: `Host`, configuratie, meniul consolei, `DeliveryZones` |
| `Laborator5_PSSC.Domain.Tests/` | Teste pentru validare si workflow, cu porturi false si ceas fix |
| `Directory.Build.props` | Setari comune: warnings ca erori, analizoare de cod, stil verificat la build |
| `Directory.Packages.props` | Versiunile pachetelor NuGet, intr-un singur loc (Central Package Management) |
| `SQL/update-db.sql` | Adauga coloana `Stock` si produsele demo |
| `compose.yaml` | Porneste SQL Server-ul din Lab 4 si ruleaza `update-db.sql` |

## Decizii de proiectare

- **SQL-first**: schema e in scripturi SQL (`Laborator4_PSSC/SQL/create-db.sql` + `SQL/update-db.sql`), iar `ShoppingCartContext` doar mapeaza tabelele in `OnModelCreating`. Nu se folosesc migrari EF.
- **Aceeasi baza ca in Lab 4**: `compose.yaml` include compose-ul din Lab 4 si foloseste acelasi nume de proiect, deci acelasi container si acelasi volum de date. Lab 5 doar extinde schema cu coloana `Stock` (stocul este necesar pentru verificare) si adauga produsele din Lab 3.
- **Porturi si adaptoare**: domeniul defineste interfetele de care are nevoie; implementarile EF Core stau in `Data/`. In teste porturile sunt inlocuite cu implementari in memorie.
- **Upsert dupa codul clientului**: comanda contine un cod de client. Daca exista deja un client cu acel cod, i se actualizeaza numele; altfel se creeaza.
- **Id-ul comenzii il da baza**: `OrderPlacedEvent` nu mai are id; `OrderId` vine din coloana `IDENTITY` la salvare.
- **Stocul se verifica de doua ori**: o data in workflow (pe datele incarcate) si inca o data atomic la salvare (`UPDATE ... WHERE Stock >= cantitate`), ca doua comenzi simultane sa nu duca stocul sub zero. Daca a doua verificare esueaza, tranzactia se anuleaza si se intoarce `InsufficientStock`.
- **Liniile cu acelasi produs se aduna**: tabela `OrderItem` permite un produs o singura data pe comanda.
- **Model de citire separat**: afisarea foloseste `IShopQueries`, care proiecteaza direct in record-uri simple (`AsNoTracking`), fara tipurile de domeniu.
- **Gazda generica cu dependency injection**: `Host.CreateApplicationBuilder` citeste configuratia (`appsettings.json`, user-secrets, variabile de mediu); `AddShoppingCartData` inregistreaza `DbContext`-ul si adaptoarele ca *scoped*. Fiecare actiune din meniu are propriul scope, deci propriul `DbContext`.
- **`TimeProvider` in loc de `DateTime.Now`**: ora comenzii vine dintr-un ceas injectat; testele folosesc `FixedTimeProvider`.
- **`CancellationToken`** pe toate metodele asincrone; Ctrl+C anuleaza operatia in curs.
- **Reguli de cod stricte**: `Directory.Build.props` transforma warnings in erori si porneste analizoarele (`latest-recommended`), doar pentru proiectele din Lab 5.

## Pornire

Necesita configurarea din Laboratorul 4 (Docker Desktop, `Laborator4_PSSC/.env`, connection string-ul in user-secrets). Lab 5 foloseste acelasi `UserSecretsId`, deci nu trebuie setat din nou.

Din radacina solutiei:

```bash
./run.sh      # optiunea 5
```

sau manual:

```bash
docker compose -f Laborator5_PSSC/compose.yaml up -d
dotnet run --project Laborator5_PSSC/Laborator5_PSSC.ConsoleApp
```

Connection string-ul se poate da si prin variabila de mediu `ConnectionStrings__DefaultConnection`.

## Utilizare

```
1. Afiseaza catalogul
2. Plaseaza o comanda
3. Afiseaza comenzile
0. Iesire
```

Produse demo: `MER-0001` (stoc 50), `LAP-0042` (10), `PAI-0007` (5), `CAF-0100` (0, pentru testarea stocului insuficient). Livrare in: Timisoara, Arad, Cluj-Napoca, Bucuresti, Iasi.

Exemplu de comanda: cod client `C-100`, nume `Ana Pop`, strada `Str. Lalelelor 5`, oras `Timisoara`, cod postal `300001`, produs `MER-0001` cantitate `3`.

## Teste

```bash
dotnet test Laborator5_PSSC/Laborator5_PSSC.Domain.Tests
```

Testele pentru validare si pentru workflow folosesc repository-uri in memorie (`FakeProductsRepository`, `RecordingOrdersRepository`) si un ceas fix, deci nu au nevoie de baza de date. `PlaceOrderWorkflow.Place` se testeaza direct, fara nicio dependinta.
