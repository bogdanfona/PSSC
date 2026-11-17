# ADR-0001: Rezervarea unui spatiu trece prin doua etape de erori (validare, apoi reguli), iar starea finala isi poarta decizia de aprobare ca tip-suma

* **Stare**: Acceptata
* **Data**: 2026-11-11
* **Lucrarea**: 9 - Proiectarea asistata de AI ([README](../../README.md))
* **Continua**: ADR-0002 din Lucrarea 3 (operatii pure compuse in workflow)

## Context si problema

Workflow-ul `ReserveSpace` primeste o cerere ca text si trebuie sa decida daca rezervarea se face. Problemele posibile sunt de doua feluri:

* **campuri gresite**: matricol, cod de spatiu, data/ora, numar de persoane, spatiu inexistent;
* **reguli incalcate** de o cerere bine formata: interval deja inceput, student blocat, capacitate, sala ocupata, suprapunere cu alta rezervare a studentului, limita zilnica.

O cerere acceptata poate fi confirmata automat (cu reminder) sau poate astepta aprobarea bibliotecarului (laboratoare, intervale de peste 2 h).

## Optiuni considerate

1. O singura validare care verifica si campurile si regulile.
2. **Doua etape: `Validate` (campuri, cu `Result.Combine`), apoi `CheckRules` (reguli), legate cu `Bind`.**
3. Reguli verificate cu `Bind` una dupa alta (se opreste la prima).

Pentru starea finala: doua stari separate (`Confirmed`, `PendingApproval`) **sau** o stare `Placed` cu un camp `Status` si campuri nullable **sau** **o stare `Placed` cu `ReservationApproval` ca tip-suma inchis**.

## Decizie

Optiunea 2 si `Placed` cu `ReservationApproval`.

* `ReservationValidation.Validate` combina cele patru campuri independente cu `Result.Combine` (toate erorile, cate una per camp) si verifica existenta spatiului cu `Ensure`.
* `ReservationRules.CheckRules` ruleaza doar pe o cerere valida: regulile au nevoie de valori bine formate (o capacitate nu se compara cu „zero” ca text). Regulile sunt independente intre ele, deci se raporteaza toate (`Result.Check`).
* Workflow-ul leaga etapele cu `Bind`: o eroare de validare sare peste reguli. `ReserveSpaceError` are un caz pentru fiecare etapa (`Validation`, `RulesViolated`).
* `ReservationPlacement.Place` este totala: orice cerere verificata se poate plasa. Decizia de aprobare este `ReservationApproval.AutoConfirmed(RemindAt)` sau `AwaitingApproval(Reason)`: un reminder exista doar pentru o rezervare confirmata, un motiv doar pentru una in asteptare.
* Codul rezervarii este derivat din spatiu si ora de inceput (`LIB-2-IND-05@202611121100`): este unic pentru ca un spatiu nu are rezervari suprapuse, si nu cere un generator aleator in nucleul pur.
* Regulile Penalizarilor (neprezentare dupa 15 minute, blocare dupa 3 neprezentari in 30 de zile) sunt functii pure separate (`NoShowPolicy`); workflow-ul de rezervare primeste doar rezultatul lor, „blocat pana la”, ca dependinta.

## Consecinte

### Pozitive

* Studentul vede toate problemele de format deodata, apoi toate regulile incalcate deodata - nu le descopera una cate una.
* O rezervare plasata nu poate avea combinatii imposibile (confirmata fara reminder, in asteptare cu reminder).
* Fiecare etapa se testeaza separat, fara dependinte ascunse.

### Negative / compromisuri

* O cerere cu un camp gresit si o regula incalcata raporteaza doar campul; regula apare la urmatoarea incercare. Acceptabil: regulile depind de valorile corecte.
* Evenimentul `SpaceReservedEvent` este plat (`Status`, `RemindAt` nullable), pentru alte contexte; tipul-suma ramane in domeniu.
* Suprapunerile se verifica pe datele incarcate; doua cereri simultane pentru acelasi interval ar trece amandoua fara o constrangere in baza (de adaugat la persistenta, ca in Lab 5).
