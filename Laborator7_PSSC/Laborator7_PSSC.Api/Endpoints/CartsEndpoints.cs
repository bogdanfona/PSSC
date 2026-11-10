using Laborator7_PSSC.Api.Clients;
using Laborator7_PSSC.Api.Mapping;
using Laborator7_PSSC.Api.Models;
using Laborator7_PSSC.Contracts.Deliveries;
using Laborator7_PSSC.Data.Entities;
using Laborator7_PSSC.Data.Queries;
using Laborator7_PSSC.Domain.Models;
using Laborator7_PSSC.Domain.Workflows;
using Laborator7_PSSC.Functional;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Laborator7_PSSC.Api.Endpoints;

// HTTP entry points for the shopping cart. A cart is a placed order: products can be added while it is unpaid.
// Each return type lists every response the endpoint can give, and the OpenAPI document is built from it.
// Paying a cart ends the order processing: the delivery API is then called to schedule the delivery.
public static partial class CartsEndpoints
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
            .WithSummary("Marcheaza cosul ca platit, apoi programeaza livrarea prin API-ul de livrari.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status502BadGateway);

        group.MapPost("/{id:int}/delivery", ScheduleDeliveryAsync)
            .WithName("ScheduleCartDelivery")
            .WithSummary("Programeaza din nou livrarea unui cos platit (daca la plata API-ul de livrari nu a raspuns).")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status502BadGateway);

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

    // The payment is saved first; the delivery is scheduled afterwards, over HTTP, with retries.
    private static async Task<Results<Ok<PaidCartResponse>, ProblemHttpResult>> PayAsync(
        int id, PayCartWorkflow workflow, IShopQueries queries, DeliveryApiClient deliveryApi,
        ILogger<DeliveryApiClient> logger, CancellationToken cancellationToken)
    {
        var result = await workflow.ExecuteAsync(new OrderId(id), cancellationToken);
        if (result is Result<CartPaidEvent, PayCartError>.Failure f)
            return f.Error.ToHttp();

        var cart = await ReadCart(new OrderId(id), queries, cancellationToken);
        return await ScheduleDelivery(cart, deliveryApi, logger, cancellationToken) switch
        {
            { } delivery => TypedResults.Ok(new PaidCartResponse(cart, delivery)),
            null => ErrorMapping.DeliveryUnavailable(cart.Id, paid: true),
        };
    }

    private static async Task<Results<Ok<DeliveryConfirmation>, ProblemHttpResult>> ScheduleDeliveryAsync(
        int id, IShopQueries queries, DeliveryApiClient deliveryApi, ILogger<DeliveryApiClient> logger, CancellationToken cancellationToken)
    {
        var cart = await queries.GetCartAsync(id, cancellationToken);
        if (cart is null)
            return ErrorMapping.CartNotFound(new OrderId(id));
        if (cart.Status != OrderStatus.Paid)
            return ErrorMapping.CartNotPaid(new OrderId(id));

        return await ScheduleDelivery(cart, deliveryApi, logger, cancellationToken) switch
        {
            { } delivery => TypedResults.Ok(delivery),
            null => ErrorMapping.DeliveryUnavailable(cart.Id, paid: false),
        };
    }

    // Null when the delivery API still fails after every retry. The payment is already saved by then,
    // so the failure is reported (502) instead of hiding it or undoing the payment.
    private static async Task<DeliveryConfirmation?> ScheduleDelivery(
        CartView cart, DeliveryApiClient deliveryApi, ILogger logger, CancellationToken cancellationToken)
    {
        try
        {
            return await deliveryApi.ScheduleAsync(cart.ToDeliveryRequest(), cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or Polly.Timeout.TimeoutRejectedException && !cancellationToken.IsCancellationRequested)
        {
            LogDeliveryFailed(logger, ex, cart.Id);
            return null;
        }
    }

    // The cart was just saved, so it exists; the response shows it as stored.
    private static async Task<CartView> ReadCart(OrderId id, IShopQueries queries, CancellationToken cancellationToken) =>
        await queries.GetCartAsync(id.Value, cancellationToken)
        ?? throw new InvalidOperationException($"Cosul {id} tocmai salvat nu a fost gasit.");

    [LoggerMessage(Level = LogLevel.Error, Message = "The delivery for cart {CartId} could not be scheduled after every retry")]
    private static partial void LogDeliveryFailed(ILogger logger, Exception exception, int cartId);
}

// Answer to a successful payment: the paid cart and the delivery scheduled for it.
public sealed record PaidCartResponse(CartView Cart, DeliveryConfirmation Delivery);
