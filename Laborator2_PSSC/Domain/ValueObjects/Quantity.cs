namespace Laborator2_PSSC.Domain.ValueObjects;

public sealed record Quantity
{
    public const int Min = 1;
    public const int Max = 100;

    public int Value { get; }

    private Quantity(int value) => Value = value;

    public static Result<Quantity> Create(int value) =>
        value is >= Min and <= Max
            ? Result.Ok(new Quantity(value))
            : Result.Fail<Quantity>($"Cantitatea trebuie sa fie intre {Min} si {Max}.");

    public static Result<Quantity> Parse(string? input) =>
        int.TryParse(input?.Trim(), out var value)
            ? Create(value)
            : Result.Fail<Quantity>($"Cantitate invalida '{input}'.");

    public override string ToString() => Value.ToString();
}
