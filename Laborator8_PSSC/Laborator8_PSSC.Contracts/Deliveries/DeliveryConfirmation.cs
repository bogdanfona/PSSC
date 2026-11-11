namespace Laborator8_PSSC.Contracts.Deliveries;

// Returned by the delivery API: the delivery scheduled for a paid cart.
public sealed record DeliveryConfirmation(
    int CartId,
    string TrackingNumber,
    string DeliveryAddress,
    DateOnly EstimatedDeliveryDate,
    DateTimeOffset CreatedAt);
