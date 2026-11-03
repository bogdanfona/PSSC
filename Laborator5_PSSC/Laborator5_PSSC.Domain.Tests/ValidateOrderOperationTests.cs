using Laborator5_PSSC.Functional;
using Laborator5_PSSC.Domain.Models;
using Laborator5_PSSC.Domain.Operations;

namespace Laborator5_PSSC.Domain.Tests;

public class ValidateOrderOperationTests
{
    [Fact]
    public void Execute_ValidInput_ReturnsValidatedOrder()
    {
        var input = new UnvalidatedOrder(
            "C-100", "Ana Pop", "Str. Lalelelor 5", "Timisoara", "300001",
            [new UnvalidatedOrderLine("mer-0001", "3"), new UnvalidatedOrderLine("LAP-0042", "2")]);

        var result = ValidateOrderOperation.Execute(input);

        var success = Assert.IsType<Result<ValidatedOrder, PlaceOrderError>.Success>(result);
        Assert.Equal("C-100", success.Value.CustomerCode.Value);
        Assert.Equal("Ana Pop", success.Value.CustomerName.Value);
        Assert.Equal("300001", success.Value.ShippingAddress.PostalCode);
        Assert.Collection(success.Value.Lines,
            l => { Assert.Equal("MER-0001", l.ProductCode.Value); Assert.Equal(3, l.Quantity.Value); },
            l => { Assert.Equal("LAP-0042", l.ProductCode.Value); Assert.Equal(2, l.Quantity.Value); });
    }

    [Fact]
    public void Execute_InvalidInput_ReturnsInvalidInputWithAllErrors()
    {
        var input = new UnvalidatedOrder(
            "cod client", "", "Str. Lalelelor 5", "Timisoara", "3000",
            [new UnvalidatedOrderLine("mere", "0")]);

        var result = ValidateOrderOperation.Execute(input);

        var failure = Assert.IsType<Result<ValidatedOrder, PlaceOrderError>.Failure>(result);
        var error = Assert.IsType<PlaceOrderError.InvalidInput>(failure.Error);
        Assert.Equal(5, error.Reasons.Count);
        Assert.Contains(error.Reasons, r => r.Contains("Cod client"));
        Assert.Contains(error.Reasons, r => r.Contains("Numele clientului"));
        Assert.Contains(error.Reasons, r => r.Contains("Cod postal"));
        Assert.Contains(error.Reasons, r => r.StartsWith("Linia 1: Cod produs", StringComparison.Ordinal));
        Assert.Contains(error.Reasons, r => r.StartsWith("Linia 1: Cantitate", StringComparison.Ordinal));
    }
}
