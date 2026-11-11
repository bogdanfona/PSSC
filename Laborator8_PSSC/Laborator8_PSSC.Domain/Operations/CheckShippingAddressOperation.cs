using Laborator8_PSSC.Functional;
using Laborator8_PSSC.Domain.Models;

namespace Laborator8_PSSC.Domain.Operations;

public static class CheckShippingAddressOperation
{
    public static Result<ValidatedOrder, PlaceOrderError> Execute(ValidatedOrder order, CheckAddressDeliverable isDeliverable) =>
        isDeliverable(order.ShippingAddress)
            ? new Result<ValidatedOrder, PlaceOrderError>.Success(order)
            : new Result<ValidatedOrder, PlaceOrderError>.Failure(new PlaceOrderError.UndeliverableAddress(order.ShippingAddress));
}
