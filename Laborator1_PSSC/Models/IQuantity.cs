namespace Laborator1_PSSC.Models;

public interface IQuantity;

public record UnitQuantity(int Units) : IQuantity
{
    public override string ToString() => $"{Units} buc";
}

public record KilogramQuantity(decimal Kilograms) : IQuantity
{
    public override string ToString() => $"{Kilograms:0.###} kg";
}
