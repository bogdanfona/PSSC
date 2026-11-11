using Laborator8_PSSC.Contracts.Events;
using Laborator8_PSSC.Events.ServiceBus;
using Laborator8_PSSC.Invoicing.Domain.Repositories;
using Laborator8_PSSC.Invoicing.Domain.Workflows;
using Laborator8_PSSC.Invoicing.EventProcessor;
using Laborator8_PSSC.Invoicing.EventProcessor.Data;
using Microsoft.EntityFrameworkCore;
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

builder.Services.AddDbContext<InvoicingContext>(options => options.UseSqlServer(
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Lipseste ConnectionStrings:DefaultConnection (dotnet user-secrets, vezi README).")));
builder.Services.AddScoped<IInvoicesRepository, InvoicesRepository>();
builder.Services.AddScoped<GenerateInvoiceWorkflow>();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddAzureClients(azure => azure.AddServiceBusClient(
    builder.Configuration.GetConnectionString("ServiceBus")
    ?? throw new InvalidOperationException(
        "Lipseste ConnectionStrings:ServiceBus. Porniti emulatorul (vezi README) sau setati dotnet user-secrets / ConnectionStrings__ServiceBus.")));

builder.Services.AddServiceBusEventListener()
    .AddHandler<OrderPlacedIntegrationEvent, OrderPlacedHandler>();

builder.Services.AddHostedService<Worker>();

await builder.Build().RunAsync();
