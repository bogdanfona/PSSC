namespace Laborator8_PSSC.Contracts.Deliveries;

// Sent by the cart API to the delivery API once a cart has been paid.
// CartId identifies the delivery: sending the same cart again returns the delivery already created.
public sealed record DeliveryRequest(
    int CartId,
    string CustomerCode,
    string CustomerName,
    string DeliveryAddress,
    IReadOnlyList<DeliveryItem> Items,
    decimal Total,
    DateTime PaidAt);

public sealed record DeliveryItem(string ProductCode, string ProductName, decimal Quantity);
