using Laborator6_PSSC.Api.Mapping;
using Laborator6_PSSC.Api.Models;
using Laborator6_PSSC.Data.Queries;
using Laborator6_PSSC.Domain.Models;
using Laborator6_PSSC.Domain.Workflows;
using Laborator6_PSSC.Functional;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Laborator6_PSSC.Api.Endpoints;

// HTTP entry points for the shopping cart. A cart is a placed order: products can be added while it is unpaid.
// Each return type lists every response the endpoint can give, and the OpenAPI document is built from it.
public static class CartsEndpoints
{
    public static IEndpointRouteBuilder MapCarts(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/carts").WithTags("Carts");

        group.MapGet("/", GetAllAsync)
            .WithName("GetCarts")
            .WithSummary("Toate cosurile (model de citire).");

        group.MapGet("/{id:int}", GetAsync)
            .WithName("GetCart")
            .WithSummary("Vizualizeaza cosul de cumparaturi.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", PlaceOrderAsync)
            .WithName("PlaceOrder")
            .WithSummary("Plaseaza o comanda; comanda devine un cos neplatit.")
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:int}/items", AddProductAsync)
            .WithName("AddProductToCart")
            .WithSummary("Adauga un produs in cos.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:int}/pay", PayAsync)
            .WithName("PayCart")
            .WithSummary("Marcheaza cosul ca platit.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    private static async Task<Ok<IReadOnlyList<CartListItem>>> GetAllAsync(IShopQueries queries, CancellationToken cancellationToken) =>
        TypedResults.Ok(await queries.GetCartsAsync(cancellationToken));

    // Reads straight from the read model; the domain is not involved in viewing.
    private static async Task<Results<Ok<CartView>, ProblemHttpResult>> GetAsync(
        int id, IShopQueries queries, CancellationToken cancellationToken) =>
        await queries.GetCartAsync(id, cancellationToken) is { } cart
            ? TypedResults.Ok(cart)
            : ErrorMapping.CartNotFound(new OrderId(id));

    private static async Task<Results<Created<CartView>, ValidationProblem, ProblemHttpResult>> PlaceOrderAsync(
        PlaceOrderRequest request, PlaceOrderWorkflow workflow, IShopQueries queries, CancellationToken cancellationToken)
    {
        var result = await workflow.ExecuteAsync(request.ToUnvalidated(), cancellationToken);
        return result switch
        {
            Result<SavedOrder, PlaceOrderError>.Success s =>
                TypedResults.Created($"/carts/{s.Value.OrderId}", await ReadCart(s.Value.OrderId, queries, cancellationToken)),
            Result<SavedOrder, PlaceOrderError>.Failure f => f.Error.ToHttp<Created<CartView>>(),
            _ => throw new InvalidOperationException(),
        };
    }

    private static async Task<Results<Ok<CartView>, ValidationProblem, ProblemHttpResult>> AddProductAsync(
        int id, AddCartItemRequest request, AddProductToCartWorkflow workflow, IShopQueries queries, CancellationToken cancellationToken)
    {
        var result = await workflow.ExecuteAsync(new OrderId(id), request.ToUnvalidated(), cancellationToken);
        return result switch
        {
            Result<ProductAddedToCartEvent, AddProductToCartError>.Success s =>
                TypedResults.Ok(await ReadCart(s.Value.CartId, queries, cancellationToken)),
            Result<ProductAddedToCartEvent, AddProductToCartError>.Failure f => f.Error.ToHttp<Ok<CartView>>(),
            _ => throw new InvalidOperationException(),
        };
    }

    private static async Task<Results<Ok<CartView>, ProblemHttpResult>> PayAsync(
        int id, PayCartWorkflow workflow, IShopQueries queries, CancellationToken cancellationToken)
    {
        var result = await workflow.ExecuteAsync(new OrderId(id), cancellationToken);
        return result switch
        {
            Result<CartPaidEvent, PayCartError>.Success s => TypedResults.Ok(await ReadCart(s.Value.CartId, queries, cancellationToken)),
            Result<CartPaidEvent, PayCartError>.Failure f => f.Error.ToHttp(),
            _ => throw new InvalidOperationException(),
        };
    }

    // The cart was just saved, so it exists; the response shows it as stored.
    private static async Task<CartView> ReadCart(OrderId id, IShopQueries queries, CancellationToken cancellationToken) =>
        await queries.GetCartAsync(id.Value, cancellationToken)
        ?? throw new InvalidOperationException($"Cosul {id} tocmai salvat nu a fost gasit.");
}
