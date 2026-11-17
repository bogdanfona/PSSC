using System.Globalization;
using Laborator9_PSSC.Domain.ValueObjects;

namespace Laborator9_PSSC.Domain.Errors;

/// <summary>One reason a field of the request is not acceptable. Closed hierarchy, one case per field or check.</summary>
public abstract record ValidationError
{
    private ValidationError() { }

    public sealed record InvalidStudentId(StudentIdError Error) : ValidationError;
    public sealed record InvalidSpaceId(SpaceIdError Error) : ValidationError;
    public sealed record UnknownSpace(SpaceId Space) : ValidationError;
    public sealed record InvalidTimeSlot(TimeSlotError Error) : ValidationError;
    public sealed record InvalidAttendees(HeadcountError Error) : ValidationError;

    public string ToMessage() => this switch
    {
        InvalidStudentId e => $"Numar matricol invalid: '{e.Error.Raw}' (format LM12345).",
        InvalidSpaceId { Error: SpaceIdError.Missing } => "Spatiul lipseste.",
        InvalidSpaceId { Error: SpaceIdError.InvalidFormat f } => $"Cod de spatiu invalid: '{f.Raw}' (format LIB-2-IND-05).",
        UnknownSpace e => $"Spatiul {e.Space} nu exista in catalog.",
        InvalidTimeSlot e => TimeSlotMessage(e.Error),
        InvalidAttendees { Error: HeadcountError.NotANumber n } => $"Numarul de persoane '{n.Raw}' nu este un numar intreg.",
        InvalidAttendees { Error: HeadcountError.OutOfRange r } =>
            $"Numarul de persoane {r.Value} trebuie sa fie intre {Headcount.Minimum} si {Headcount.Maximum}.",
        _ => GetType().Name,
    };

    private static string TimeSlotMessage(TimeSlotError error) => error switch
    {
        TimeSlotError.InvalidDate e => $"Data '{e.Raw}' nu este valida (format yyyy-MM-dd).",
        TimeSlotError.InvalidTime e => $"Ora '{e.Raw}' nu este valida (format HH:mm).",
        TimeSlotError.NotOnHalfHour e => $"Ora {Time(e.Time)} nu este la fix sau la jumatate.",
        TimeSlotError.EndNotAfterStart e => $"Sfarsitul ({Time(e.End)}) trebuie sa fie dupa inceput ({Time(e.Start)}).",
        TimeSlotError.OutsideOpeningHours e =>
            $"Intervalul {Time(e.Start)}-{Time(e.End)} iese din programul {Time(TimeSlot.Opens)}-{Time(TimeSlot.Closes)}.",
        TimeSlotError.TooShort e => $"Durata minima este 30 de minute (cerut {e.Duration.TotalMinutes:0} min).",
        TimeSlotError.TooLong e => $"Durata maxima este 4 ore (cerut {e.Duration.TotalHours:0.#} h).",
        _ => error.GetType().Name,
    };

    private static string Time(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);
}
