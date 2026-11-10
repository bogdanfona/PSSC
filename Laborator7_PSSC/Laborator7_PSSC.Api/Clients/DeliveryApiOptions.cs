using System.ComponentModel.DataAnnotations;

namespace Laborator7_PSSC.Api.Clients;

// Settings for the HTTP client to the delivery API, bound from the "DeliveryApi" section and checked at startup.
public sealed class DeliveryApiOptions
{
    public const string SectionName = "DeliveryApi";

    [Required]
    public Uri BaseAddress { get; set; } = null!;

    public RetryOptions Retry { get; set; } = new();

    // Limit for a single attempt; a slower answer counts as a transient error and is retried.
    [Range(1, 60)]
    public int AttemptTimeoutSeconds { get; set; } = 5;

    public sealed class RetryOptions
    {
        [Range(0, 10)]
        public int MaxRetryAttempts { get; set; } = 3;

        // Delay before the first retry; it doubles on each retry (600 ms, 1.2 s, 2.4 s), plus jitter.
        [Range(50, 10_000)]
        public int BaseDelayMilliseconds { get; set; } = 600;
    }
}
