using System.Collections.Concurrent;
using Laborator7_PSSC.Contracts.Deliveries;

namespace Laborator7_PSSC.Delivery;

// The deliveries, kept in memory: the delivery context has its own data and does not touch the shop database.
// One delivery per cart, so a request repeated by a retry returns the delivery it already created.
public sealed class DeliveryStore(TimeProvider clock)
{
    private readonly ConcurrentDictionary<int, DeliveryConfirmation> deliveries = new();

    // Created is false when the cart already had a delivery.
    public (DeliveryConfirmation Delivery, bool Created) Schedule(DeliveryRequest request)
    {
        var created = false;
        var delivery = deliveries.GetOrAdd(request.CartId, _ =>
        {
            created = true;
            var now = clock.GetLocalNow();
            return new DeliveryConfirmation(
                request.CartId,
                $"AWB-{request.CartId:D6}",
                request.DeliveryAddress,
                DateOnly.FromDateTime(now.DateTime).AddDays(2),
                now);
        });
        return (delivery, created);
    }

    public DeliveryConfirmation? Find(int cartId) => deliveries.GetValueOrDefault(cartId);

    public IReadOnlyList<DeliveryConfirmation> All() => [.. deliveries.Values.OrderBy(d => d.CartId)];
}
