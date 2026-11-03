# Laborator 6 - Folosirea unui model DDD pentru a implementa un API Web

Workflow-urile cosului de cumparaturi expuse printr-un API Web ASP.NET Core minimal, peste aceeasi baza de date `ShoppingCart` ca Laboratoarele 4 si 5. Documentul OpenAPI este generat automat (`/openapi/v1.json`), iar Swagger UI (`/swagger`) permite testarea din browser.

Analiza solutiei din `Exemple` (Sarcina 1) este in [Sarcina1.md](Sarcina1.md).

## Cosul de cumparaturi

Un cos este o comanda plasata (un rand din tabela `Order`), cu o stare noua:

```
POST /carts            ->  Unpaid  --POST /carts/{id}/items-->  Unpaid (mai multe produse)
(PlaceOrderWorkflow)          |
                              +--POST /carts/{id}/pay-->  Paid  (nu mai accepta produse)
```

## Endpoint-uri (Sarcina 2)

| Metoda si ruta | Ce face | Raspunsuri |
|---|---|---|
| `GET /carts/{id}` | **Vizualizeaza cosul**: client, adresa, produse, total, stare | 200, 404 |
| `POST /carts/{id}/items` | **Adauga un produs in cos** (`{"productCode": "MER-0001", "quantity": 2}`); daca produsul e deja in cos, cantitatea se aduna | 200, 400, 404, 409 |
| `POST /carts/{id}/pay` | **Marcheaza cosul platit** | 200, 404, 409 |
| `POST /carts` | Plaseaza o comanda (workflow-ul din Lab 3/5); comanda devine un cos neplatit | 201, 400, 409 |
| `GET /carts` | Toate cosurile, cu starea si totalul | 200 |
| `GET /products` | Catalogul, cu stocul curent | 200 |

Raspunsurile de eroare sunt in format standard [RFC 9457](https://www.rfc-editor.org/rfc/rfc9457) (problem details):

| Cod | Cand |
|---|---|
| 400 `ValidationProblem` | date invalide, produs inexistent, oras fara livrare; toate erorile sunt raportate deodata, grupate dupa tip |
| 400 | corp JSON invalid sau de tip gresit |
| 404 | cosul nu exista |
| 409 | cosul este deja platit, cosul este gol (la plata) sau stocul nu ajunge |
| 500 | eroare neprevazuta (de exemplu baza de date oprita), cu `traceId` |

## Structura

| Proiect | Rol |
|---|---|
| `Laborator6_PSSC.Functional/` | `Result<T, TError>` |
| `Laborator6_PSSC.Domain/` | Domeniul din Lab 5, plus `Cart` (`Unpaid` / `Paid`), erorile si evenimentele cosului |
| `Laborator6_PSSC.Domain/Workflows/` | `PlaceOrderWorkflow`, `AddProductToCartWorkflow`, `PayCartWorkflow`: fiecare cu `ExecuteAsync` (citire -> pur -> scriere) si o functie pura statica (`Place`, `AddProduct`, `Pay`) |
| `Laborator6_PSSC.Data/` | EF Core: `CartsRepository` (adaptor pentru `ICartsRepository`), modelul de citire `CartView` |
| `Laborator6_PSSC.Api/` | API-ul: `Program.cs`, endpoint-uri, DTO-uri de intrare, maparea erorilor la HTTP |
| `Laborator6_PSSC.Domain.Tests/` | Teste pentru toate cele trei workflow-uri, fara baza de date |
| `SQL/update-db.sql` | Adauga `Status` (`Unpaid` / `Paid`) si `PaidAt` in tabela `Order` |
| `compose.yaml` | Porneste SQL Server-ul din Lab 4, ruleaza scripturile din Lab 5 si Lab 6 |

## Decizii de proiectare

- **Cosul este comanda**: nu exista o tabela separata; `Order.Status` spune daca se mai pot adauga produse. Comenzile vechi au primit `Unpaid`. O constrangere `CHECK` garanteaza ca un cos platit are `PaidAt`, iar unul neplatit nu.
- **Endpoint = functie, cu raspunsuri tipizate**: tipul de retur (`Results<Ok<CartView>, ValidationProblem, ProblemHttpResult>`) enumera raspunsurile posibile; din el si din `ProducesProblem` se genereaza documentul OpenAPI.
- **Validarea doar in domeniu**: DTO-urile (`PlaceOrderRequest`, `AddCartItemRequest`) au toate campurile optionale si nicio regula; `RequestMapping` le transforma in text si le preda domeniului, care raporteaza toate problemele.
- **Erorile domeniului -> HTTP intr-un singur loc** (`ErrorMapping`): date gresite -> 400, cos inexistent -> 404, starea cosului sau stocul nu permit operatia -> 409.
- **Vizualizarea ocoleste domeniul**: `GET /carts/{id}` citeste direct modelul de citire `CartView`; dupa adaugare si plata, raspunsul este tot `CartView`, recitit din baza.
- **Verificari atomice la salvare**: workflow-ul decide pe starea incarcata, iar `CartsRepository` repeta atomic verificarile care pot fi invalidate intre timp: plata schimba doar un cos `Unpaid` (`UPDATE ... WHERE Status = 'Unpaid'`), adaugarea blocheaza randul cosului si scade stocul doar daca ajunge, intr-o tranzactie.
- **Cosurile din Lab 4** pot contine produse vandute la kilogram, cu coduri si cantitati pe care regulile de azi nu le-ar accepta; de aceea `CartLine` pastreaza codul si cantitatea asa cum sunt in baza.

## Pornire

Necesita configurarea din Laboratorul 4 (Docker Desktop, `Laborator4_PSSC/.env`, connection string-ul in user-secrets). Toate laboratoarele folosesc acelasi `UserSecretsId`.

```bash
./run.sh      # optiunea 6
```

sau manual:

```bash
docker compose -f Laborator6_PSSC/compose.yaml up -d
dotnet run --project Laborator6_PSSC/Laborator6_PSSC.Api
```

Se deschide `https://localhost:7196/swagger`. Daca browserul avertizeaza ca certificatul nu este de incredere, se ruleaza o data `dotnet dev-certs https --trust`.

Exemplu cu `curl`:

```bash
curl -k -X POST https://localhost:7196/carts -H 'Content-Type: application/json' \
  -d '{"customerCode":"C-400","customerName":"Dan Rusu","street":"Str. Mare 3","city":"Iasi","postalCode":"700002","lines":[{"productCode":"MER-0001","quantity":2}]}'
curl -k -X POST https://localhost:7196/carts/<id>/items -H 'Content-Type: application/json' -d '{"productCode":"PAI-0007","quantity":1}'
curl -k https://localhost:7196/carts/<id>
curl -k -X POST https://localhost:7196/carts/<id>/pay
```

## Teste

```bash
dotnet test Laborator6_PSSC/Laborator6_PSSC.Domain.Tests
```
