using Laborator6_PSSC.Functional;
using Laborator6_PSSC.Domain.Models;

namespace Laborator6_PSSC.Domain.Operations;

public static class CheckShippingAddressOperation
{
    public static Result<ValidatedOrder, PlaceOrderError> Execute(ValidatedOrder order, CheckAddressDeliverable isDeliverable) =>
        isDeliverable(order.ShippingAddress)
            ? new Result<ValidatedOrder, PlaceOrderError>.Success(order)
            : new Result<ValidatedOrder, PlaceOrderError>.Failure(new PlaceOrderError.UndeliverableAddress(order.ShippingAddress));
}
