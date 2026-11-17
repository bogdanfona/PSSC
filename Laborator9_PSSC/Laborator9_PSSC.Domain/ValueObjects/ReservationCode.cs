using System.Globalization;
using System.Text.RegularExpressions;
using Laborator9_PSSC.Functional;

namespace Laborator9_PSSC.Domain.ValueObjects;

/// <summary>
/// Identifies a reservation: the space and the start, for example "LIB-2-IND-05@202611121000". A space cannot hold two
/// overlapping reservations, so this is unique, and it is derived from the reservation itself (no random id in the pure core).
/// </summary>
public sealed partial record ReservationCode
{
    [GeneratedRegex("^(?<space>(LIB|DORM)-[0-5]-(IND|GRP|LAB)-[0-9]{2})@(?<start>[0-9]{12})$")]
    private static partial Regex Pattern { get; }

    public string Value { get; }

    private ReservationCode(string value) => Value = value;

    public static ReservationCode For(SpaceId space, TimeSlot slot) =>
        new(string.Create(CultureInfo.InvariantCulture, $"{space}@{slot.StartsAt:yyyyMMddHHmm}"));

    public static Result<ReservationCode, ReservationCodeError> Parse(string? raw)
    {
        var value = raw?.Trim().ToUpperInvariant();
        return value is not null && Pattern.Match(value) is { Success: true } match
            && DateTime.TryParseExact(match.Groups["start"].Value, "yyyyMMddHHmm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
            ? new ReservationCode(value)
            : new ReservationCodeError(raw);
    }

    public override string ToString() => Value;
}

public sealed record ReservationCodeError(string? Raw);
