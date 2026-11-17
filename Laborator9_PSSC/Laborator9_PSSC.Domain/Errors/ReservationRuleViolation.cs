using System.Globalization;
using Laborator9_PSSC.Domain.ValueObjects;

namespace Laborator9_PSSC.Domain.Errors;

/// <summary>A business rule the request breaks, although every field is well formed. Closed hierarchy.</summary>
public abstract record ReservationRuleViolation
{
    private ReservationRuleViolation() { }

    public sealed record StartsInPast(TimeSlot Slot) : ReservationRuleViolation;
    public sealed record StudentBlocked(StudentId Student, DateTimeOffset Until) : ReservationRuleViolation;
    public sealed record OverCapacity(int Attendees, int Capacity) : ReservationRuleViolation;
    public sealed record IndividualSpaceForOnePerson(int Attendees) : ReservationRuleViolation;
    public sealed record GroupSpaceNeedsGroup(int Attendees) : ReservationRuleViolation;
    public sealed record SlotTaken(SpaceId Space, TimeSlot Existing) : ReservationRuleViolation;
    public sealed record StudentAlreadyBooked(SpaceId Space, TimeSlot Existing) : ReservationRuleViolation;
    public sealed record DailyLimitExceeded(SpaceType Type, TimeSpan AlreadyBooked, TimeSpan Requested, TimeSpan Limit) : ReservationRuleViolation;

    public string ToMessage() => this switch
    {
        StartsInPast e => $"Intervalul {e.Slot} a inceput deja.",
        StudentBlocked e => string.Create(CultureInfo.InvariantCulture,
            $"Studentul {e.Student} este blocat pana la {e.Until:yyyy-MM-dd HH:mm} (3 neprezentari)."),
        OverCapacity e => $"Spatiul are {e.Capacity} locuri; s-au cerut {e.Attendees}.",
        IndividualSpaceForOnePerson e => $"Un spatiu individual este pentru o singura persoana (cerut {e.Attendees}).",
        GroupSpaceNeedsGroup e => $"O sala de grup se rezerva pentru cel putin 2 persoane (cerut {e.Attendees}).",
        SlotTaken e => $"Spatiul {e.Space} este deja rezervat in intervalul {e.Existing}.",
        StudentAlreadyBooked e => $"Studentul are deja rezervat {e.Space} in intervalul {e.Existing}.",
        DailyLimitExceeded e => string.Create(CultureInfo.InvariantCulture,
            $"Limita zilnica pentru spatii {Name(e.Type)} este {e.Limit.TotalHours:0.#} h; rezervat deja {e.AlreadyBooked.TotalHours:0.#} h, cerut {e.Requested.TotalHours:0.#} h."),
        _ => GetType().Name,
    };

    private static string Name(SpaceType type) => type == SpaceType.Individual ? "individuale" : "de grup";
}
