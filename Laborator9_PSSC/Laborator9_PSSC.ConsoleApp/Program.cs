using System.Globalization;
using System.Text;
using Laborator9_PSSC.ConsoleApp;
using Laborator9_PSSC.Domain.Commands;
using Laborator9_PSSC.Domain.Events;
using Laborator9_PSSC.Domain.Operations;
using Laborator9_PSSC.Domain.States;
using Laborator9_PSSC.Domain.ValueObjects;
using Laborator9_PSSC.Domain.Workflows;

Console.OutputEncoding = Encoding.UTF8;
Console.WriteLine("=== Rezervarea spatiilor de studiu (Laborator 9) ===");
Console.WriteLine("Workflow-ul ReserveSpace: Unvalidated -> Validated -> Checked -> Placed, cu dependente in memorie.");

// A fixed clock, so the demo gives the same answers whenever it runs: Thursday 12 November 2026, 09:00, campus time.
var now = new DateTimeOffset(2026, 11, 12, 9, 0, 0, TimeSpan.FromHours(2));
Console.WriteLine($"Ora curenta (fixa pentru demo): {now:yyyy-MM-dd HH:mm}");

// --- Mock dependencies, hard-coded ---
var catalog = new[]
{
    Data.Space("LIB-2-IND-05", 1),
    Data.Space("LIB-2-IND-06", 1),
    Data.Space("LIB-1-GRP-03", 6),
    Data.Space("DORM-3-GRP-12", 8),
    Data.Space("LIB-1-LAB-01", 12),
}.ToDictionary(s => s.Id);

IReadOnlyList<ExistingReservation> existing =
[
    // LM10001 already holds an individual space for one hour this morning.
    Data.Booking("LIB-2-IND-06", "LM10001", "2026-11-12", "09:30", "10:30"),
    // The group room is taken in the afternoon.
    Data.Booking("LIB-1-GRP-03", "LM10002", "2026-11-12", "14:00", "16:00"),
];

// Penalties context: LM10003 missed three reservations in the last 30 days.
IReadOnlyList<DateTimeOffset> noShowsOfLm10003 = [now.AddDays(-20), now.AddDays(-9), now.AddDays(-2)];
var blockedUntil = new Dictionary<StudentId, DateTimeOffset>();
NoShowPolicy.BlockedUntil(noShowsOfLm10003, now).Match(
    until => blockedUntil[Data.Student("LM10003")] = until,
    () => default);

// --- Test cases ---
(string Title, ReserveSpaceCommand Command)[] cases =
[
    ("Valid: spatiu individual, 1 h, confirmat automat",
        new("LM10004", "LIB-2-IND-05", "2026-11-12", "11:00", "12:00", "1")),
    ("Valid: sala de grup, 3 h, 4 persoane -> necesita aprobare (peste 2 h)",
        new("LM10005", "DORM-3-GRP-12", "2026-11-13", "10:00", "13:00", "4")),
    ("Valid: laborator, 1 h -> necesita aprobare (laborator)",
        new("LM10006", "LIB-1-LAB-01", "2026-11-13", "16:00", "17:00", "5")),
    ("Limita: LM10001 are deja 1 h individual; cere inca 1 h, lipit de rezervarea existenta (total exact 2 h)",
        new("LM10001", "LIB-2-IND-05", "2026-11-12", "10:30", "11:30", "1")),
    ("Invalid: toate campurile gresite",
        new("lm1", "MALL-1-IND-01", "12.11.2026", "10:15", "09:00", "zero")),
    ("Invalid: interval gresit si spatiu care nu exista",
        new("LM10007", "LIB-4-IND-99", "2026-11-12", "21:00", "23:00", "1")),
    ("Reguli incalcate: student blocat, sala ocupata, prea multe persoane",
        new("LM10003", "LIB-1-GRP-03", "2026-11-12", "15:00", "17:00", "9")),
    ("Reguli incalcate: limita zilnica (1 h existenta + 1,5 h cerute > 2 h) si interval deja inceput",
        new("LM10001", "LIB-2-IND-05", "2026-11-12", "08:30", "10:00", "1")),
];

foreach (var (title, command) in cases)
{
    Console.WriteLine();
    Console.WriteLine(new string('-', 90));
    Console.WriteLine($"▶ {title}");
    Console.WriteLine($"  Intrare: student={command.StudentId}, spatiu={command.SpaceId}, {command.Date} {command.Start}-{command.End}, persoane={command.Attendees}");

    var result = ReserveSpaceWorkflow.Publish(command, catalog, existing, blockedUntil, now);

    Console.WriteLine(result.Match(
        reserved => Data.Describe(reserved),
        error => "  ❌ Respins:" + Environment.NewLine + string.Join(Environment.NewLine, error.ToMessages().Select(m => $"     - {m}"))));
}

// --- The impure shell: same workflow through in-memory ports; a saved reservation blocks the next identical one ---
Console.WriteLine();
Console.WriteLine(new string('=', 90));
Console.WriteLine("▶ ExecuteAsync cu porturi in memorie: aceeasi cerere de doua ori");
var repository = new InMemoryReservations(existing);
var workflow = new ReserveSpaceWorkflow(new InMemorySpaces(catalog), repository, new InMemoryStanding(blockedUntil), new FixedClock(now));
var request = new ReserveSpaceCommand("LM10008", "LIB-2-IND-05", "2026-11-12", "18:00", "19:00", "1");
foreach (var attempt in new[] { 1, 2 })
{
    var result = await workflow.ExecuteAsync(request, CancellationToken.None);
    Console.WriteLine($"  Incercarea {attempt}: " + result.Match(
        reserved => $"✅ {reserved.ReservationCode} salvata",
        error => "❌ " + string.Join(" ", error.ToMessages())));
}
Console.WriteLine($"  Rezervari salvate: {repository.Saved.Count}");

namespace Laborator9_PSSC.ConsoleApp
{
    internal static class Data
    {
        public static StudySpace Space(string id, int capacity) =>
            new(Parse(SpaceId.Parse(id)), Parse(Headcount.Create(capacity)));

        public static StudentId Student(string id) => Parse(StudentId.Parse(id));

        public static ExistingReservation Booking(string space, string student, string date, string start, string end) =>
            new(Parse(SpaceId.Parse(space)), Student(student), Parse(TimeSlot.Parse(date, start, end)));

        // Hard-coded demo data is known to be valid; a failure here is a bug in the demo, so it throws.
        private static T Parse<T, TError>(Laborator9_PSSC.Functional.Result<T, TError> result)
            where T : notnull
            where TError : notnull =>
            result.Match(value => value, error => throw new InvalidOperationException($"Date demo invalide: {error}"));

        public static string Describe(SpaceReservedEvent e) =>
            string.Create(CultureInfo.InvariantCulture, $"  ✅ Rezervare {e.ReservationCode}: {e.SpaceId}, {e.Date:yyyy-MM-dd} {e.Start:HH\\:mm}-{e.End:HH\\:mm}, {e.Attendees} pers.")
            + Environment.NewLine
            + (e.Status == ReservationStatus.Confirmed
                ? string.Create(CultureInfo.InvariantCulture, $"     Confirmata automat; reminder la {e.RemindAt:HH\\:mm}.")
                : "     ⏳ Asteapta aprobarea bibliotecarului.");
    }
}
