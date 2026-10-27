namespace Laborator2_PSSC.Domain.ValueObjects;

public readonly record struct CartId(Guid Value)
{
    public static CartId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString()[..8];
}

public readonly record struct CustomerId(Guid Value)
{
    public static CustomerId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString()[..8];
}
