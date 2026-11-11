using Laborator8_PSSC.Api;
using Azure.Messaging.ServiceBus;
using Laborator8_PSSC.Api.Endpoints;
using Laborator8_PSSC.Api.Messaging;
using Laborator8_PSSC.Data;
using Laborator8_PSSC.Domain.Models;
using Laborator8_PSSC.Domain.Workflows;
using Laborator8_PSSC.Events.ServiceBus;
using Microsoft.Extensions.Azure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddShoppingCartData(
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Lipseste ConnectionStrings:DefaultConnection (dotnet user-secrets, vezi README)."));

builder.Services.AddScoped<PlaceOrderWorkflow>();
builder.Services.AddScoped<AddProductToCartWorkflow>();
builder.Services.AddScoped<PayCartWorkflow>();
builder.Services.AddSingleton<CheckAddressDeliverable>(DeliveryZones.IsDeliverable);
builder.Services.AddSingleton(TimeProvider.System);

// Events go out through Azure Service Bus (the local emulator in Development; see appsettings.Development.json).
builder.Services
    .AddOptions<MessagingOptions>()
    .Bind(builder.Configuration.GetSection(MessagingOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddAzureClients(azure => azure
    .AddServiceBusClient(builder.Configuration.GetConnectionString("ServiceBus")
        ?? throw new InvalidOperationException(
            "Lipseste ConnectionStrings:ServiceBus. Porniti emulatorul (vezi README) sau setati dotnet user-secrets / ConnectionStrings__ServiceBus."))
    // The client retries on its own; a short try timeout keeps a down broker from blocking a payment for minutes.
    .ConfigureOptions(options =>
    {
        options.RetryOptions.MaxRetries = 2;
        options.RetryOptions.TryTimeout = TimeSpan.FromSeconds(5);
    }));
builder.Services.AddServiceBusEventSender(builder.Configuration);

builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.RespectNullableAnnotations = true);
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier);

builder.Services.AddOpenApi();

var app = builder.Build();

// Unhandled exceptions (database down, ...) become 500 problem details with a traceId.
// A malformed request body (invalid JSON, wrong type) keeps its own status, 400.
app.UseExceptionHandler(new ExceptionHandlerOptions
{
    StatusCodeSelector = exception => exception is BadHttpRequestException bad ? bad.StatusCode : StatusCodes.Status500InternalServerError,
});
app.UseStatusCodePages();
app.UseHttpsRedirection();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Laborator8_PSSC.Api v1"));
}

app.MapProducts();
app.MapCarts();

app.Run();
