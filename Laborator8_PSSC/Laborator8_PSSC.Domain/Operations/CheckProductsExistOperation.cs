using Laborator8_PSSC.Functional;
using Laborator8_PSSC.Domain.Models;

namespace Laborator8_PSSC.Domain.Operations;

public static class CheckProductsExistOperation
{
    public static Result<ValidatedOrder, PlaceOrderError> Execute(ValidatedOrder order, CheckProductExists productExists)
    {
        var missing = order.Lines
            .Select(l => l.ProductCode)
            .Distinct()
            .Where(code => !productExists(code))
            .ToList();

        return missing.Count == 0
            ? new Result<ValidatedOrder, PlaceOrderError>.Success(order)
            : new Result<ValidatedOrder, PlaceOrderError>.Failure(new PlaceOrderError.ProductsNotFound(missing));
    }
}
