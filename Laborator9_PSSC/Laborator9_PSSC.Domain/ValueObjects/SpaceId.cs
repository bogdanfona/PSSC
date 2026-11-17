using System.Globalization;
using System.Text.RegularExpressions;
using Laborator9_PSSC.Functional;

namespace Laborator9_PSSC.Domain.ValueObjects;

/// <summary>What a space is for; it decides the daily limit and whether a reservation needs approval.</summary>
public enum SpaceType
{
    Individual,
    Group,
    Lab,
}

/// <summary>
/// Identifies a study space: Building-Floor-Type-Number, for example "LIB-2-IND-05" or "DORM-3-GRP-12".
/// Building LIB (library) or DORM (dormitory), floor 0-5, type IND / GRP / LAB, number 01-99.
/// </summary>
public sealed partial record SpaceId
{
    [GeneratedRegex("^(LIB|DORM)-([0-5])-(IND|GRP|LAB)-(0[1-9]|[1-9][0-9])$")]
    private static partial Regex Pattern { get; }

    public string Value { get; }
    public string Building { get; }
    public int Floor { get; }
    public SpaceType Type { get; }

    private SpaceId(string value, string building, int floor, SpaceType type)
    {
        Value = value;
        Building = building;
        Floor = floor;
        Type = type;
    }

    public static Result<SpaceId, SpaceIdError> Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new SpaceIdError.Missing();

        var value = raw.Trim().ToUpperInvariant();
        var match = Pattern.Match(value);
        if (!match.Success)
            return new SpaceIdError.InvalidFormat(raw);

        var type = match.Groups[3].Value switch
        {
            "IND" => SpaceType.Individual,
            "GRP" => SpaceType.Group,
            _ => SpaceType.Lab,
        };
        return new SpaceId(value, match.Groups[1].Value, int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture), type);
    }

    public override string ToString() => Value;
}

public abstract record SpaceIdError
{
    private SpaceIdError() { }

    public sealed record Missing : SpaceIdError;
    public sealed record InvalidFormat(string Raw) : SpaceIdError;
}
