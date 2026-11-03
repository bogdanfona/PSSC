# Laborator 4 - Shopping cart cu EF Core si SQL Server

Aplicatie consola care gestioneaza produse, clienti si comenzi intr-o baza de date SQL Server, folosind Entity Framework Core.

Abordarea este **SQL-first**: tabelele sunt create de scriptul `SQL/create-db.sql`, iar `ShoppingCartContext` doar le mapeaza pe clasele din `Models/`.

## Structura

| Fisier | Rol |
|---|---|
| `Program.cs` | Meniul consolei si operatiile (adaugare / afisare) |
| `Data/ShoppingCartContext.cs` | `DbContext`-ul EF Core si maparea pe tabele |
| `Models/` | Entitatile `Product`, `Customer`, `Order`, `OrderItem` |
| `SQL/create-db.sql` | Creeaza baza `ShoppingCart` si tabelele (poate fi rulat de mai multe ori) |
| `compose.yaml` | Porneste SQL Server in Docker si ruleaza scriptul de creare |
| `.env.example` | Model pentru fisierul `.env` cu parola de `sa` |

## Baza de date

- `Products` - `Code` unic, `Price`, `QuantityType` (`Unit` sau `Kg`)
- `Customers` - `Code` unic, `Name`
- `Order` - data, adresa de livrare, clientul
- `OrderItem` - produsul si cantitatea dintr-o comanda; un produs apare o singura data pe comanda (`UNIQUE (OrderId, ProductId)`)

## Cerinte

- .NET 10 SDK
- Docker Desktop (pe Mac cu Apple Silicon: Settings > General > bifat *Use Rosetta for x86_64/amd64 emulation*, pentru ca imaginea SQL Server exista doar pentru amd64)

## Pornire

1. Creeaza fisierul `.env` si alege o parola puternica (minim 8 caractere, litere mari, mici, cifre si simboluri):

   ```bash
   cp .env.example .env
   ```

2. Porneste SQL Server si creeaza baza de date:

   ```bash
   docker compose up -d
   ```

   Containerul `sqlserver-init` asteapta ca serverul sa fie pornit, ruleaza `SQL/create-db.sql` si se opreste. Verificare:

   ```bash
   docker compose ps -a
   ```

   `sqlserver-init` trebuie sa apara cu `Exited (0)`.

3. Seteaza connection string-ul cu aceeasi parola ca in `.env` (se salveaza in user-secrets, nu in proiect):

   ```bash
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost,1433;Database=ShoppingCart;User Id=sa;Password=<parola>;TrustServerCertificate=true"
   ```

   Alternativ, se poate da prin variabila de mediu `ConnectionStrings__DefaultConnection`.

4. Ruleaza aplicatia:

   ```bash
   dotnet run
   ```

## Utilizare

```
1. Adauga produs
2. Adauga client
3. Adauga comanda
4. Adauga produs in comanda
5. Afiseaza produse
6. Afiseaza clienti
7. Afiseaza comenzi
8. Afiseaza produse din comenzi
0. Iesire
```

Ordinea fireasca: produs -> client -> comanda (pe codul clientului) -> produse in comanda (pe id-ul comenzii si codul produsului).

- Codurile de produs/client sunt transformate in majuscule si trebuie sa fie unice.
- Produsele `Unit` accepta doar cantitati intregi; cele `Kg` accepta zecimale (`2.5` sau `2,5`).
- Daca adaugi un produs care e deja in comanda, cantitatea se aduna la cea existenta.
- Afisarea comenzilor include totalul calculat din produsele comenzii.
- Comenzile SQL trimise de EF Core sunt afisate in consola.

## Oprire si resetare

```bash
docker compose down      # opreste containerele, pastreaza datele
docker compose down -v   # sterge si volumul cu datele (baza o ia de la zero)
```
