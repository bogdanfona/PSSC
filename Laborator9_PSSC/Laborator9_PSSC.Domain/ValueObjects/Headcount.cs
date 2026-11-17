using System.Globalization;
using Laborator9_PSSC.Functional;

namespace Laborator9_PSSC.Domain.ValueObjects;

/// <summary>A number of people: how many come to a reservation, or how many a space holds. 1 to 30.</summary>
public sealed record Headcount
{
    public const int Minimum = 1;
    public const int Maximum = 30;

    public int Value { get; }

    private Headcount(int value) => Value = value;

    public static Result<Headcount, HeadcountError> Create(int value) =>
        value is >= Minimum and <= Maximum ? new Headcount(value) : new HeadcountError.OutOfRange(value);

    public static Result<Headcount, HeadcountError> Parse(string? raw) =>
        int.TryParse(raw?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? Create(value)
            : new HeadcountError.NotANumber(raw);

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

public abstract record HeadcountError
{
    private HeadcountError() { }

    public sealed record NotANumber(string? Raw) : HeadcountError;
    public sealed record OutOfRange(int Value) : HeadcountError;
}
