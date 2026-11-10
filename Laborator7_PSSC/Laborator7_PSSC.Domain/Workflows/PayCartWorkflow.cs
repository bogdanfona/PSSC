using Laborator7_PSSC.Domain.Models;
using Laborator7_PSSC.Domain.Repositories;
using Laborator7_PSSC.Functional;

namespace Laborator7_PSSC.Domain.Workflows;

// Impure -> pure -> impure, like PlaceOrderWorkflow.
public sealed class PayCartWorkflow(ICartsRepository cartsRepository, TimeProvider clock)
{
    public async Task<Result<CartPaidEvent, PayCartError>> ExecuteAsync(OrderId cartId, CancellationToken cancellationToken)
    {
        var cart = await cartsRepository.GetAsync(cartId, cancellationToken);

        return Pay(cartId, cart, clock.GetLocalNow().DateTime) switch
        {
            Result<CartPaidEvent, PayCartError>.Success s => await cartsRepository.MarkPaidAsync(s.Value, cancellationToken),
            var failure => failure
        };
    }

    // The pure core: only an existing, unpaid cart with at least one product can be paid.
    public static Result<CartPaidEvent, PayCartError> Pay(OrderId cartId, Cart? cart, DateTime now) => cart switch
    {
        null => new Result<CartPaidEvent, PayCartError>.Failure(new PayCartError.CartNotFound(cartId)),
        Cart.Paid paid => new Result<CartPaidEvent, PayCartError>.Failure(new PayCartError.CartAlreadyPaid(cartId, paid.PaidAt)),
        { Lines.Count: 0 } => new Result<CartPaidEvent, PayCartError>.Failure(new PayCartError.CartEmpty(cartId)),
        _ => new Result<CartPaidEvent, PayCartError>.Success(new CartPaidEvent(cartId, cart.Total, now)),
    };
}
