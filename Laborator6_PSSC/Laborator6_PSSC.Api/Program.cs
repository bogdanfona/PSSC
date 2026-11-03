using Laborator6_PSSC.Api;
using Laborator6_PSSC.Api.Endpoints;
using Laborator6_PSSC.Data;
using Laborator6_PSSC.Domain.Models;
using Laborator6_PSSC.Domain.Workflows;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddShoppingCartData(
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Lipseste ConnectionStrings:DefaultConnection (dotnet user-secrets, vezi README)."));

builder.Services.AddScoped<PlaceOrderWorkflow>();
builder.Services.AddScoped<AddProductToCartWorkflow>();
builder.Services.AddScoped<PayCartWorkflow>();
builder.Services.AddSingleton<CheckAddressDeliverable>(DeliveryZones.IsDeliverable);
builder.Services.AddSingleton(TimeProvider.System);

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
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Laborator6_PSSC.Api v1"));
}

app.MapProducts();
app.MapCarts();

app.Run();
