using System.Globalization;
using Laborator6_PSSC.Api.Models;
using Laborator6_PSSC.Domain.Models;

namespace Laborator6_PSSC.Api.Mapping;

// From the JSON shapes to the raw types the domain accepts. The domain parses text, so numbers are
// written back with a dot as decimal separator, whatever the server culture.
public static class RequestMapping
{
    public static UnvalidatedOrder ToUnvalidated(this PlaceOrderRequest request) => new(
        request.CustomerCode ?? "",
        request.CustomerName ?? "",
        request.Street ?? "",
        request.City ?? "",
        request.PostalCode ?? "",
        [.. (request.Lines ?? []).Select(l => new UnvalidatedOrderLine(l.ProductCode ?? "", Text(l.Quantity)))]);

    public static UnvalidatedCartItem ToUnvalidated(this AddCartItemRequest request) =>
        new(request.ProductCode, Text(request.Quantity));

    private static string Text(decimal? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "";
}
