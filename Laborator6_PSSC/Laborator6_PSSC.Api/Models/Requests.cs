namespace Laborator6_PSSC.Api.Models;

// JSON shapes accepted at the boundary. Deliberately loose: every field is optional and nothing repeats
// the domain rules (code format, quantity range, postal code). The domain checks them and reports every problem.

public sealed record OrderLineRequest(string? ProductCode, decimal? Quantity);

public sealed record PlaceOrderRequest(
    string? CustomerCode,
    string? CustomerName,
    string? Street,
    string? City,
    string? PostalCode,
    IReadOnlyList<OrderLineRequest>? Lines);

public sealed record AddCartItemRequest(string? ProductCode, decimal? Quantity);
