using Laborator3_PSSC.Domain.Models;

namespace Laborator3_PSSC.Domain.Operations;

public static class CheckStockOperation
{
    // The same product can appear on several lines, so quantities are summed per product.
    public static Result<ValidatedOrder, PlaceOrderError> Execute(ValidatedOrder order, GetStock getStock)
    {
        var shortage = order.Lines
            .GroupBy(l => l.ProductCode)
            .Select(g => (Code: g.Key, Requested: g.Sum(l => l.Quantity.Value), Available: getStock(g.Key)))
            .FirstOrDefault(x => x.Requested > x.Available);

        return shortage.Code is null
            ? new Result<ValidatedOrder, PlaceOrderError>.Success(order)
            : new Result<ValidatedOrder, PlaceOrderError>.Failure(
                new PlaceOrderError.InsufficientStock(shortage.Code, shortage.Requested, shortage.Available));
    }
}
