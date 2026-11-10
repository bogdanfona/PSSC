using Laborator7_PSSC.Functional;
using Laborator7_PSSC.Domain.Models;

namespace Laborator7_PSSC.Domain.Operations;

public static class ValidateOrderOperation
{
    // Converts raw input into domain types, collecting all validation errors.
    public static Result<ValidatedOrder, PlaceOrderError> Execute(UnvalidatedOrder order)
    {
        var errors = new List<string>();

        var customerCode = Collect(CustomerCode.Create(order.CustomerCode), errors);
        var name = Collect(CustomerName.Create(order.CustomerName), errors);
        var address = Collect(Address.Create(order.Street, order.City, order.PostalCode), errors);

        if (order.Lines.Count == 0)
            errors.Add("Comanda trebuie sa contina cel putin un produs.");

        var lines = new List<ValidatedOrderLine>();
        for (var i = 0; i < order.Lines.Count; i++)
        {
            var lineErrors = new List<string>();
            var code = Collect(ProductCode.Create(order.Lines[i].ProductCode), lineErrors);
            var quantity = Collect(Quantity.Parse(order.Lines[i].Quantity), lineErrors);

            if (code is not null && quantity is not null)
                lines.Add(new ValidatedOrderLine(code, quantity));
            errors.AddRange(lineErrors.Select(e => $"Linia {i + 1}: {e}"));
        }

        return errors.Count == 0
            ? new Result<ValidatedOrder, PlaceOrderError>.Success(new ValidatedOrder(customerCode!, name!, address!, lines))
            : new Result<ValidatedOrder, PlaceOrderError>.Failure(new PlaceOrderError.InvalidInput(errors));
    }

    private static T? Collect<T>(Result<T, string> result, List<string> errors) where T : class
    {
        switch (result)
        {
            case Result<T, string>.Success s:
                return s.Value;
            case Result<T, string>.Failure f:
                errors.Add(f.Error);
                return null;
            default:
                throw new InvalidOperationException();
        }
    }

    private static T? Collect<T>(Result<T, IReadOnlyList<string>> result, List<string> errors) where T : class
    {
        switch (result)
        {
            case Result<T, IReadOnlyList<string>>.Success s:
                return s.Value;
            case Result<T, IReadOnlyList<string>>.Failure f:
                errors.AddRange(f.Error);
                return null;
            default:
                throw new InvalidOperationException();
        }
    }
}
