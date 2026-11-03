# Laborator 5 - Interactiunea cu baza de date

Workflow-ul de plasare a comenzii din Laboratorul 3, legat de baza de date `ShoppingCart` din Laboratorul 4 (EF Core 10, abordare SQL-first).

Fluxul unei comenzi, cu I/O doar la margini:

```
citire din baza  ->  workflow pur  ->  scriere in baza
(ProductsRepository)  (PlaceOrderWorkflow)  (OrdersRepository)
```

1. **Inainte de workflow** se incarca din baza doar produsele din comanda (cod, nume, pret, stoc) intr-un `ProductCatalog`.
2. **Workflow-ul** (validare -> produse existente -> stoc -> adresa -> pret) este acelasi ca in Laboratorul 3. Functiile `CheckProductExists`, `GetStock` si `GetProductPrice` sunt acum metodele `ProductCatalog`, deci folosesc date din baza, dar workflow-ul ramane pur (nu face I/O).
3. **Dupa workflow**, daca comanda a fost plasata, `OrdersRepository` o salveaza intr-o singura tranzactie.

## Structura

| Fisier | Rol |
|---|---|
| `Domain/` | Modelul de domeniu din Lab 3: value objects, operatii, workflow, `Result` |
| `Domain/Models/ProductCatalog.cs` | Starea produselor incarcata din baza inainte de workflow |
| `Domain/Repositories/` | Porturile domeniului: `IProductsRepository`, `IOrdersRepository` |
| `Data/Repositories/` | Adaptoarele EF Core care implementeaza porturile |
| `Data/ShoppingCartContext.cs`, `Data/Entities/` | `DbContext`-ul si entitatile din Lab 4 (plus `Stock`) |
| `ReadModels/`, `Data/Queries/` | Modelul de citire separat pentru afisarea catalogului si a comenzilor |
| `Application/PlaceOrderService.cs` | Leaga pasii: citire -> workflow -> scriere |
| `Infrastructure/DeliveryZones.cs` | Orasele in care se livreaza (nu sunt in baza) |
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

## Pornire

Necesita configurarea din Laboratorul 4 (Docker Desktop, `Laborator4_PSSC/.env`, connection string-ul in user-secrets). Lab 5 foloseste acelasi `UserSecretsId`, deci nu trebuie setat din nou.

Din radacina solutiei:

```bash
./run.sh      # optiunea 5
```

sau manual:

```bash
docker compose -f Laborator5_PSSC/compose.yaml up -d
dotnet run --project Laborator5_PSSC
```

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
dotnet test Laborator5_PSSC.Tests
```

Testele pentru validare si pentru `PlaceOrderService` folosesc repository-uri in memorie, deci nu au nevoie de baza de date.
