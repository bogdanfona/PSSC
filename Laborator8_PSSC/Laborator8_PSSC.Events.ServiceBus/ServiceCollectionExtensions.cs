using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Laborator8_PSSC.Events.ServiceBus;

public static class ServiceCollectionExtensions
{
    // IEventSender over Service Bus. Needs a ServiceBusClient already registered (AddAzureClients).
    public static IServiceCollection AddServiceBusEventSender(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ServiceBusEventsOptions>().Bind(configuration.GetSection(ServiceBusEventsOptions.SectionName));
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IEventSender, ServiceBusTopicEventSender>();
        return services;
    }

    // IEventListener over Service Bus; continue with AddHandler for every event type the listener must know.
    public static EventListenerBuilder AddServiceBusEventListener(this IServiceCollection services)
    {
        services.TryAddSingleton<IEventListener, ServiceBusTopicEventListener>();
        return new EventListenerBuilder(services);
    }
}

public sealed class EventListenerBuilder(IServiceCollection services)
{
    public EventListenerBuilder AddHandler<TEvent, THandler>()
        where TEvent : IIntegrationEvent
        where THandler : class, IEventHandler<TEvent>
    {
        services.AddScoped<IEventHandler<TEvent>, THandler>();
        services.AddSingleton<IEventDispatcher, EventDispatcher<TEvent>>();
        return this;
    }
}
