using Laborator8_PSSC.Contracts.Deliveries;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Laborator8_PSSC.Delivery.Endpoints;

// The delivery context's HTTP entry points.
public static partial class DeliveriesEndpoints
{
    public static IEndpointRouteBuilder MapDeliveries(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/deliveries").WithTags("Deliveries");

        group.MapPost("/", Schedule)
            .WithName("ScheduleDelivery")
            .WithSummary("Programeaza livrarea unui cos platit. Repetarea cererii pentru acelasi cos intoarce aceeasi livrare.")
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/", GetAll)
            .WithName("GetDeliveries")
            .WithSummary("Toate livrarile programate.");

        group.MapGet("/{cartId:int}", Get)
            .WithName("GetDelivery")
            .WithSummary("Livrarea unui cos.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static Results<Created<DeliveryConfirmation>, Ok<DeliveryConfirmation>, ValidationProblem, ProblemHttpResult> Schedule(
        DeliveryRequest request, DeliveryStore store, FailureSimulator simulator, ILogger<DeliveryStore> logger)
    {
        var (attempt, fail) = simulator.Register(request.CartId);
        if (fail)
        {
            LogSimulatedFailure(logger, request.CartId, attempt, simulator.FailFirstAttempts);
            return TypedResults.Problem(
                "Serviciul de livrari este temporar indisponibil (eroare simulata).",
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Serviciu indisponibil.");
        }

        var errors = Validate(request);
        if (errors.Count > 0)
            return TypedResults.ValidationProblem(errors, title: "Livrarea nu poate fi programata.");

        var (delivery, created) = store.Schedule(request);
        LogScheduled(logger, delivery.TrackingNumber, request.CartId, attempt, created);
        return created
            ? TypedResults.Created($"/deliveries/{delivery.CartId}", delivery)
            : TypedResults.Ok(delivery);
    }

    private static Ok<IReadOnlyList<DeliveryConfirmation>> GetAll(DeliveryStore store) => TypedResults.Ok(store.All());

    private static Results<Ok<DeliveryConfirmation>, ProblemHttpResult> Get(int cartId, DeliveryStore store) =>
        store.Find(cartId) is { } delivery
            ? TypedResults.Ok(delivery)
            : TypedResults.Problem($"Nu exista livrare pentru cosul {cartId}.", statusCode: StatusCodes.Status404NotFound, title: "Livrare inexistenta.");

    private static Dictionary<string, string[]> Validate(DeliveryRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.CartId <= 0)
            errors[nameof(request.CartId)] = ["Id-ul cosului trebuie sa fie pozitiv."];
        if (string.IsNullOrWhiteSpace(request.DeliveryAddress))
            errors[nameof(request.DeliveryAddress)] = ["Adresa de livrare lipseste."];
        if (request.Items is not { Count: > 0 })
            errors[nameof(request.Items)] = ["Nu exista produse de livrat."];
        return errors;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Cart {CartId}: attempt {Attempt} fails on purpose (Delivery:FailFirstAttempts = {FailFirstAttempts})")]
    private static partial void LogSimulatedFailure(ILogger logger, int cartId, int attempt, int failFirstAttempts);

    [LoggerMessage(Level = LogLevel.Information, Message = "Delivery {TrackingNumber} for cart {CartId} on attempt {Attempt} (new: {Created})")]
    private static partial void LogScheduled(ILogger logger, string trackingNumber, int cartId, int attempt, bool created);
}
