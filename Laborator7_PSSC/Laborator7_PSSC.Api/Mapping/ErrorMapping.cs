using System.Diagnostics;
using Laborator7_PSSC.Domain.Models;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Laborator7_PSSC.Api.Mapping;

// Workflow errors -> HTTP, in one place (RFC 9457 problem details):
// bad input -> 400 ValidationProblem, missing cart -> 404, cart or stock state forbids it -> 409,
// delivery API down after every retry -> 502.
// TSuccess is the endpoint's success response, so the result fits the endpoint's return type as is.
public static class ErrorMapping
{
    public static Results<TSuccess, ValidationProblem, ProblemHttpResult> ToHttp<TSuccess>(this PlaceOrderError error)
        where TSuccess : IResult => error switch
    {
        PlaceOrderError.InvalidInput e => Invalid(nameof(PlaceOrderError.InvalidInput), [.. e.Reasons]),
        PlaceOrderError.ProductsNotFound e => Invalid(nameof(PlaceOrderError.ProductsNotFound),
            [.. e.Codes.Select(c => $"Produs inexistent: {c}.")]),
        PlaceOrderError.UndeliverableAddress e => Invalid(nameof(PlaceOrderError.UndeliverableAddress),
            [$"Nu livram la adresa {e.Address}."]),
        PlaceOrderError.InsufficientStock e => Conflict(StockMessage(e.Code, e.Requested, e.Available)),
        _ => throw new UnreachableException(),
    };

    public static Results<TSuccess, ValidationProblem, ProblemHttpResult> ToHttp<TSuccess>(this AddProductToCartError error)
        where TSuccess : IResult => error switch
    {
        AddProductToCartError.InvalidInput e => Invalid(nameof(AddProductToCartError.InvalidInput), [.. e.Reasons]),
        AddProductToCartError.ProductNotFound e => Invalid(nameof(AddProductToCartError.ProductNotFound),
            [$"Produs inexistent: {e.Code}."]),
        AddProductToCartError.CartNotFound e => CartNotFound(e.CartId),
        AddProductToCartError.CartAlreadyPaid e => Conflict($"Cosul {e.CartId} este deja platit; nu se mai pot adauga produse."),
        AddProductToCartError.InsufficientStock e => Conflict(StockMessage(e.Code, e.Requested, e.Available)),
        _ => throw new UnreachableException(),
    };

    public static ProblemHttpResult ToHttp(this PayCartError error) => error switch
    {
        PayCartError.CartNotFound e => CartNotFound(e.CartId),
        PayCartError.CartAlreadyPaid e => Conflict($"Cosul {e.CartId} a fost deja platit la {e.PaidAt:dd.MM.yyyy HH:mm}."),
        PayCartError.CartEmpty e => Conflict($"Cosul {e.CartId} este gol; nu are ce sa fie platit."),
        _ => throw new UnreachableException(),
    };

    public static ProblemHttpResult CartNotPaid(OrderId id) =>
        Conflict($"Cosul {id} nu este platit; livrarea se programeaza doar dupa plata.");

    // The delivery API did not answer even after the retries: 502, the failing dependency is not hidden.
    public static ProblemHttpResult DeliveryUnavailable(int cartId, bool paid) => TypedResults.Problem(
        (paid ? $"Cosul {cartId} a fost platit, dar livrarea nu a putut fi programata. " : $"Livrarea cosului {cartId} nu a putut fi programata. ")
            + $"Reincercati mai tarziu cu POST /carts/{cartId}/delivery.",
        statusCode: StatusCodes.Status502BadGateway,
        title: "API-ul de livrari nu raspunde.");

    public static ProblemHttpResult CartNotFound(OrderId id) =>
        TypedResults.Problem($"Nu exista cosul {id}.", statusCode: StatusCodes.Status404NotFound, title: "Cos inexistent.");

    private static ValidationProblem Invalid(string code, string[] messages) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { [code] = messages }, title: "Cererea nu este valida.");

    private static ProblemHttpResult Conflict(string detail) =>
        TypedResults.Problem(detail, statusCode: StatusCodes.Status409Conflict, title: "Operatia nu este permisa in starea curenta.");

    private static string StockMessage(ProductCode code, int requested, int available) =>
        $"Stoc insuficient pentru {code}: cerut {requested}, disponibil {available}.";
}
