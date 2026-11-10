using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;

namespace Laborator7_PSSC.Api.Clients;

public static class ServiceCollectionExtensions
{
    // Registers DeliveryApiClient with a retry policy: every transient error (5xx, 408, 429, network error,
    // attempt timeout) is retried 3 times, with exponentially growing delays and jitter. Values come from configuration.
    public static IServiceCollection AddDeliveryApiClient(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<DeliveryApiOptions>()
            .Bind(configuration.GetSection(DeliveryApiOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddHttpClient<DeliveryApiClient>((provider, client) =>
                client.BaseAddress = provider.GetRequiredService<IOptions<DeliveryApiOptions>>().Value.BaseAddress)
            .AddResilienceHandler("delivery-api", (pipeline, context) =>
            {
                var options = context.ServiceProvider.GetRequiredService<IOptions<DeliveryApiOptions>>().Value;

                // Retrying a POST is safe here: the delivery API keeps one delivery per cart,
                // so a repeated request returns the delivery already created instead of a second one.
                pipeline.AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = options.Retry.MaxRetryAttempts,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    Delay = TimeSpan.FromMilliseconds(options.Retry.BaseDelayMilliseconds),
                });

                // Added after the retry, so it limits each attempt, not the whole call.
                pipeline.AddTimeout(TimeSpan.FromSeconds(options.AttemptTimeoutSeconds));
            });

        return services;
    }
}
