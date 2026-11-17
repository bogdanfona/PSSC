using System.Globalization;
using Laborator9_PSSC.Functional;

namespace Laborator9_PSSC.Domain.ValueObjects;

/// <summary>
/// One interval on one day, in campus time. Only structural rules live here (opening hours 08:00-22:00, start and end
/// on the hour or half hour, 30 minutes to 4 hours). "In the future" needs the clock, so it is a rule of the reservation,
/// checked in an operation that receives <c>now</c> as a parameter.
/// </summary>
public sealed record TimeSlot
{
    public static readonly TimeOnly Opens = new(8, 0);
    public static readonly TimeOnly Closes = new(22, 0);
    public static readonly TimeSpan MinimumDuration = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan MaximumDuration = TimeSpan.FromHours(4);

    public DateOnly Date { get; }
    public TimeOnly Start { get; }
    public TimeOnly End { get; }

    private TimeSlot(DateOnly date, TimeOnly start, TimeOnly end)
    {
        Date = date;
        Start = start;
        End = end;
    }

    public TimeSpan Duration => End - Start;
    public DateTime StartsAt => Date.ToDateTime(Start);
    public DateTime EndsAt => Date.ToDateTime(End);

    public static Result<TimeSlot, TimeSlotError> Create(DateOnly date, TimeOnly start, TimeOnly end)
    {
        if (!OnHalfHour(start))
            return new TimeSlotError.NotOnHalfHour(start);
        if (!OnHalfHour(end))
            return new TimeSlotError.NotOnHalfHour(end);
        if (end <= start)
            return new TimeSlotError.EndNotAfterStart(start, end);
        if (start < Opens || end > Closes)
            return new TimeSlotError.OutsideOpeningHours(start, end);

        var duration = end - start;
        if (duration < MinimumDuration)
            return new TimeSlotError.TooShort(duration);
        if (duration > MaximumDuration)
            return new TimeSlotError.TooLong(duration);

        return new TimeSlot(date, start, end);
    }

    /// <summary>Date as yyyy-MM-dd, times as HH:mm, independent of the machine's culture.</summary>
    public static Result<TimeSlot, TimeSlotError> Parse(string? date, string? start, string? end)
    {
        if (!DateOnly.TryParseExact(date?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
            return new TimeSlotError.InvalidDate(date);
        if (!TimeOnly.TryParseExact(start?.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var from))
            return new TimeSlotError.InvalidTime(start);
        if (!TimeOnly.TryParseExact(end?.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var to))
            return new TimeSlotError.InvalidTime(end);
        return Create(day, from, to);
    }

    /// <summary>Same day and the intervals intersect; touching ends (10:00-12:00 and 12:00-13:00) do not overlap.</summary>
    public bool OverlapsWith(TimeSlot other) => Date == other.Date && Start < other.End && other.Start < End;

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Date:yyyy-MM-dd} {Start:HH\\:mm}-{End:HH\\:mm}");

    private static bool OnHalfHour(TimeOnly time) => time is { Second: 0, Millisecond: 0 } && time.Minute is 0 or 30;
}

public abstract record TimeSlotError
{
    private TimeSlotError() { }

    public sealed record InvalidDate(string? Raw) : TimeSlotError;
    public sealed record InvalidTime(string? Raw) : TimeSlotError;
    public sealed record NotOnHalfHour(TimeOnly Time) : TimeSlotError;
    public sealed record EndNotAfterStart(TimeOnly Start, TimeOnly End) : TimeSlotError;
    public sealed record OutsideOpeningHours(TimeOnly Start, TimeOnly End) : TimeSlotError;
    public sealed record TooShort(TimeSpan Duration) : TimeSlotError;
    public sealed record TooLong(TimeSpan Duration) : TimeSlotError;
}
