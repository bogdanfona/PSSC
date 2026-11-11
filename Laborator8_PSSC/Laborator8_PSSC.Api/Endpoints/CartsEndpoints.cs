using Azure.Messaging.ServiceBus;
using Laborator8_PSSC.Api.Mapping;
using Laborator8_PSSC.Api.Messaging;
using Laborator8_PSSC.Api.Models;
using Laborator8_PSSC.Data.Entities;
using Laborator8_PSSC.Data.Queries;
using Laborator8_PSSC.Domain.Models;
using Laborator8_PSSC.Domain.Workflows;
using Laborator8_PSSC.Events;
using Laborator8_PSSC.Functional;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace Laborator8_PSSC.Api.Endpoints;

// HTTP entry points for the shopping cart. A cart is a placed order: products can be added while it is unpaid.
// Each return type lists every response the endpoint can give, and the OpenAPI document is built from it.
// Paying a cart ends the order processing: the "order placed" event is then published on the order-placed topic,
// and invoicing and shipping react to it on their own, without the API waiting for them.
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
            .WithSummary("Marcheaza cosul ca platit si publica evenimentul de comanda preluata (facturare, livrare).")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group.MapPost("/{id:int}/order-placed", RepublishAsync)
            .WithName("RepublishOrderPlaced")
            .WithSummary("Publica din nou evenimentul pentru un cos platit (daca la plata Service Bus nu a raspuns). Consumatorii sunt idempotenti.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

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

    // The payment is saved first; the event is published afterwards. The two are not one transaction:
    // if the process stops in between, the event is lost (a transactional outbox would fix that; out of scope).
    private static async Task<Results<Ok<CartView>, ProblemHttpResult>> PayAsync(
        int id, PayCartWorkflow workflow, IShopQueries queries, IEventSender events, IOptions<MessagingOptions> messaging,
        ILogger<MessagingOptions> logger, CancellationToken cancellationToken)
    {
        var result = await workflow.ExecuteAsync(new OrderId(id), cancellationToken);
        if (result is Result<CartPaidEvent, PayCartError>.Failure f)
            return f.Error.ToHttp();

        var cart = await ReadCart(new OrderId(id), queries, cancellationToken);
        return await Publish(cart, events, messaging.Value, logger, cancellationToken)
            ? TypedResults.Ok(cart)
            : ErrorMapping.EventNotPublished(cart.Id, paid: true);
    }

    private static async Task<Results<Ok<CartView>, ProblemHttpResult>> RepublishAsync(
        int id, IShopQueries queries, IEventSender events, IOptions<MessagingOptions> messaging,
        ILogger<MessagingOptions> logger, CancellationToken cancellationToken)
    {
        var cart = await queries.GetCartAsync(id, cancellationToken);
        if (cart is null)
            return ErrorMapping.CartNotFound(new OrderId(id));
        if (cart.Status != OrderStatus.Paid)
            return ErrorMapping.CartNotPaid(new OrderId(id));

        return await Publish(cart, events, messaging.Value, logger, cancellationToken)
            ? TypedResults.Ok(cart)
            : ErrorMapping.EventNotPublished(cart.Id, paid: false);
    }

    // False when Service Bus cannot be reached even after the client's own retries. The payment is already
    // saved by then, so the failure is reported (503) instead of being hidden or undoing the payment.
    private static async Task<bool> Publish(
        CartView cart, IEventSender events, MessagingOptions messaging, ILogger logger, CancellationToken cancellationToken)
    {
        try
        {
            await events.SendAsync(new TopicName(messaging.OrderPlacedTopic), cart.ToOrderPlacedEvent(), cancellationToken);
            return true;
        }
        catch (ServiceBusException ex) when (!cancellationToken.IsCancellationRequested)
        {
            LogPublishFailed(logger, ex, cart.Id);
            return false;
        }
    }

    // The cart was just saved, so it exists; the response shows it as stored.
    private static async Task<CartView> ReadCart(OrderId id, IShopQueries queries, CancellationToken cancellationToken) =>
        await queries.GetCartAsync(id.Value, cancellationToken)
        ?? throw new InvalidOperationException($"Cosul {id} tocmai salvat nu a fost gasit.");

    [LoggerMessage(Level = LogLevel.Error, Message = "The order placed event for cart {CartId} could not be published")]
    private static partial void LogPublishFailed(ILogger logger, Exception exception, int cartId);
}
