# Rezervarea spatiilor de studiu - DDD Lab (Laborator 9)

## Echipa
**Pisicile Salbatice**
- Fona Bogdan
- Ghib Cosmin

## Domeniul Ales
Optiunea C: **Study Space Reservation** - rezervarea salilor de studiu individuale, a salilor de grup si a laboratoarelor din biblioteca si camin.

## Descriere
Studentul cere un spatiu pentru un interval. Sistemul verifica cererea (format, existenta spatiului), aplica regulile de rezervare (interval in viitor, student neblocat, capacitate, sala libera, fara suprapuneri, max 2 h/zi pentru spatii individuale si 4 h/zi pentru sali de grup) si plaseaza rezervarea: confirmata automat, cu reminder cu 30 de minute inainte, sau in asteptarea aprobarii bibliotecarului (laboratoare si intervale de peste 2 h). Regulile de penalizare (neprezentare dupa 15 minute de intarziere, blocare o saptamana dupa 3 neprezentari) sunt functii pure separate, a caror iesire o primeste workflow-ul.

## Bounded Contexts Identificate
1. **Rezervari** (core): regulile de rezervare, aprobare, check-in, neprezentare - workflow-ul implementat.
2. **Catalog spatii** (suport): spatii, tip, capacitate, echipamente, program.
3. **Penalizari** (suport): numararea neprezentarilor, blocarea studentilor.
4. **Notificari** (generic): reminder-e si mesaje de aprobare.
5. **Statistici** (raportare): grad de ocupare, rata de neprezentare.

## Event Storming Results
[docs/EventStorming.md](docs/EventStorming.md): 16 evenimente in ordine cronologica (`Event | Trigger | Data | Subsequent`), diagrama contextelor (Mermaid), comenzile, agregatele, regulile si invariantii fiecarui context.

## Implementare

Stilul urmeaza [`.github/copilot-instructions.md`](../.github/copilot-instructions.md) (copiat din curs, Sarcina 2.2): obiecte-valoare cu `Parse`/`Create` care intorc `Result`, stari ca tip-suma inchis fara `Invalid`, operatii ca membri de extensie C# 14, workflow compus cu `Map`/`Bind`.

### Value Objects
- `StudentId`: numarul matricol `LM12345` (normalizat la majuscule).
- `SpaceId`: `Cladire-Etaj-Tip-Numar`, de exemplu `LIB-2-IND-05`; expune `Building`, `Floor` si `Type` (`Individual`, `Group`, `Lab`).
- `TimeSlot`: o zi si un interval; 08:00-22:00, la fix sau la jumatate, intre 30 de minute si 4 ore; `Duration`, `OverlapsWith`.
- `Headcount`: numar de persoane, 1-30 (participantii unei rezervari, capacitatea unui spatiu).
- `ReservationCode`: `LIB-2-IND-05@202611121100`, derivat din spatiu si ora de inceput; `Parse` pentru text.

Fiecare are propriul tip de eroare (`StudentIdError`, `SpaceIdError`, `TimeSlotError`, `HeadcountError`, `ReservationCodeError`).

### Entity States
- `SpaceReservation.Unvalidated`: cererea, exact cum a fost scrisa.
- `SpaceReservation.Validated`: toate campurile sunt obiecte-valoare, spatiul exista.
- `SpaceReservation.Checked`: toate regulile de rezervare sunt respectate.
- `SpaceReservation.Placed`: rezervarea exista; `Approval` este `AutoConfirmed(RemindAt)` sau `AwaitingApproval(Reason)`.

`Match` pe `SpaceReservation` (si tipul `ReservationApproval`) da potrivire exhaustiva.

### Erori
- `ReserveSpaceError.Validation`: campuri gresite sau spatiu inexistent (lista de `ValidationError`).
- `ReserveSpaceError.RulesViolated`: cerere bine formata care incalca reguli (lista de `ReservationRuleViolation`: `StartsInPast`, `StudentBlocked`, `OverCapacity`, `IndividualSpaceForOnePerson`, `GroupSpaceNeedsGroup`, `SlotTaken`, `StudentAlreadyBooked`, `DailyLimitExceeded`).

Toate au `ToMessage()`/`ToMessages()`, in romana.

### Operatii (functii)
1. `ReservationValidation.Validate`: parseaza cele patru campuri cu `Result.Combine` (toate erorile) si verifica existenta spatiului cu `Ensure`.
2. `ReservationRules.CheckRules`: aplica regulile de rezervare; dependintele (catalog, rezervari existente, blocari, `now`) sunt parametri.
3. `ReservationPlacement.Place`: totala; decide confirmarea automata sau aprobarea si calculeaza reminder-ul.
4. `ReservationEvents.ToEvent`: `SpaceReservedEvent` din starea finala.
5. `NoShowPolicy.IsNoShow` / `BlockedUntil`: regulile Penalizarilor.

### Workflow
`ReserveSpaceWorkflow.Publish`: `Unvalidated` -> `.Validate(catalog)` -> `.MapError(Validation)` -> `.Bind(CheckRules(...).MapError(RulesViolated))` -> `.Map(Place(now))` -> `.Map(ToEvent)`. Fara logica de business, fara `try/catch`. In plus, `ExecuteAsync` (impur -> pur -> impur) incarca dependintele prin porturi, ruleaza nucleul pur si salveaza rezervarea doar pe ramura de succes.

Decizia de proiectare principala: [docs/adr/0001-rezervare-validare-si-reguli-separate.md](docs/adr/0001-rezervare-validare-si-reguli-separate.md).

## Rulare

```bash
dotnet build Laborator9_PSSC/Laborator9_PSSC.ConsoleApp
dotnet run --project Laborator9_PSSC/Laborator9_PSSC.ConsoleApp     # sau ./run.sh, optiunea 9
dotnet test Laborator9_PSSC/Laborator9_PSSC.Domain.Tests
```

Consola foloseste un ceas fix (joi, 12 noiembrie 2026, 09:00) si date in memorie, deci da mereu acelasi rezultat:

| Caz | Rezultat |
|---|---|
| Spatiu individual, 1 h | ✅ confirmat, reminder la 10:30 |
| Sala de grup, 3 h, 4 persoane | ✅ asteapta aprobare (peste 2 h) |
| Laborator, 1 h | ✅ asteapta aprobare (laborator) |
| Limita: 1 h existenta + 1 h ceruta, lipita de prima (exact 2 h) | ✅ confirmat |
| Toate campurile gresite | ❌ 4 erori, cate una per camp |
| Interval dupa 22:00 si spatiu inexistent | ❌ 2 erori |
| Student blocat, sala ocupata, 9 persoane in sala de 6 | ❌ 3 reguli incalcate |
| Interval deja inceput, suprapunere si limita zilnica depasita | ❌ 3 reguli incalcate |
| `ExecuteAsync` de doua ori cu aceeasi cerere | ✅ apoi ❌ sala ocupata; o singura rezervare salvata |

46 de teste xunit.v3: obiecte-valoare (cazuri valide, invalide, limite), validare (toate erorile, in ordine), reguli (fiecare regula, limita exacta de 2 h, blocare expirata), workflow (confirmare, aprobare, eroarea de validare sare peste reguli, salvare doar la succes) si politica de neprezentari (exact 15 minute = inca la timp).

## Lectii Invatate

### Cum a fost folosit AI-ul
Codul si documentele din acest folder au fost generate cu **Claude Code (Claude Opus 5.5)**, nu cu GitHub Copilot, pornind de la enuntul laboratorului, `copilot-instructions.md`, anexa cursului pentru domeniul C si exemplul din Lucrarea 3. Rolul echipei a fost sa aleaga domeniul si sa verifice rezultatul; sectiunea de mai jos descrie ce s-a observat in aceasta sesiune.

### Ce a functionat bine cu AI
- **Boilerplate-ul pattern-urilor**: obiectele-valoare, ierarhiile de erori si `Match` au iesit in stilul cerut din prima, pentru ca ghidul are exemple concrete.
- **Raportarea tuturor erorilor**: `Result.Combine` pentru campuri si o verificare similara (`Result.Check`) pentru regulile independente.
- **Testele au gasit o problema reala**: o cerere de 2 persoane pentru un spatiu individual raporta doua erori pentru aceeasi cauza (capacitate depasita si „spatiu pentru o persoana”); regula de capacitate se aplica acum doar spatiilor de grup.

### Limitari ale AI identificate
- **Reguli de business inventate**: enuntul nu spune tot. AI-ul a ales singur: aprobare pentru intervale de peste 2 h si pentru toate laboratoarele; o sala de grup cere cel putin 2 persoane; limitele zilnice se numara separat pentru spatii individuale si de grup; blocarea dureaza 7 zile de la ultima neprezentare, numarand pe ultimele 30 de zile. **Echipa trebuie sa confirme aceste interpretari** - sunt decizii de domeniu, nu de cod.
- **Erori de compilare si configurare**, corectate in sesiune: inferenta de tipuri esuata la `Result.Check` (a trebuit dat tipul explicit), un avertisment de analizor (CA1859) tratat ca eroare, si xunit.v3 4.x care foloseste implicit Microsoft Testing Platform, incompatibil cu restul solutiei (rezolvat cu `IsTestingPlatformApplication=false`).
- **Dependintele pe ceas**: „intervalul este in viitor” nu poate sta in `TimeSlot` (obiectele-valoare nu au dependinte externe); a fost mutat in `CheckRules`, care primeste `now` ca parametru.

### Prompturi Utile
Promptul care a pornit lucrul a fost scurt („fa laboratorul 9 cu domeniul C”), pentru ca AI-ul a citit singur ghidul si exemplele cursului. Pentru a compara cu GitHub Copilot, echipa poate rula sablonul din Sarcina 2.3 completat pentru domeniul C, de exemplu:

```
// Create value object for TimeSlot representing one reservation interval on one day, in campus time
// Validation rules:
// - Start and end on the hour or half hour; end after start
// - Inside opening hours 08:00-22:00
// - Duration between 30 minutes and 4 hours
// - "In the future" is NOT checked here (needs the clock): it is a reservation rule
// Valid examples: 2026-11-12 10:00-12:00, 2026-11-12 08:00-08:30
// Invalid examples: 10:15-11:00 (not on half hour), 21:00-23:00 (after closing), 10:00-14:30 (too long)
// Follow the pattern from copilot-instructions.md
// Include: Duration, OverlapsWith(TimeSlot other) - touching ends do not overlap
```

## Decizii de arhitectura
- [ADR-0001](docs/adr/0001-rezervare-validare-si-reguli-separate.md): validare si reguli ca doua etape de erori; decizia de aprobare ca tip-suma in starea finala; cod de rezervare derivat; regulile Penalizarilor ca functii pure separate.
