using Laborator8_PSSC.Contracts.Events;
using Laborator8_PSSC.Events.ServiceBus;
using Laborator8_PSSC.Shipping.EventProcessor;
using Laborator8_PSSC.Shipping.EventProcessor.Clients;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// appsettings*.json are read from the output folder, so the worker starts the same from any directory.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = args, ContentRootPath = AppContext.BaseDirectory });

builder.Services
    .AddOptions<EventProcessorOptions>()
    .Bind(builder.Configuration.GetSection(EventProcessorOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Typed HTTP client to the delivery API, with the retry policy from Lab 7 (DeliveryApi section).
builder.Services.AddDeliveryApiClient(builder.Configuration);

builder.Services.AddAzureClients(azure => azure.AddServiceBusClient(
    builder.Configuration.GetConnectionString("ServiceBus")
    ?? throw new InvalidOperationException(
        "Lipseste ConnectionStrings:ServiceBus. Porniti emulatorul (vezi README) sau setati dotnet user-secrets / ConnectionStrings__ServiceBus.")));

builder.Services.AddServiceBusEventListener()
    .AddHandler<OrderPlacedIntegrationEvent, OrderPlacedHandler>();

builder.Services.AddHostedService<Worker>();

await builder.Build().RunAsync();
