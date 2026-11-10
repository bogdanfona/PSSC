using System.Collections.Concurrent;

namespace Laborator7_PSSC.Delivery;

// Makes the retry policy of the caller visible: the first N requests for each cart answer 503,
// as if the service were briefly unavailable. N comes from Delivery:FailFirstAttempts (0 = never fail).
public sealed class FailureSimulator(IConfiguration configuration)
{
    private readonly ConcurrentDictionary<int, int> attempts = new();

    public int FailFirstAttempts => configuration.GetValue("Delivery:FailFirstAttempts", 0);

    // Counts the attempt and returns its number (1 = first) and whether it should fail.
    public (int Attempt, bool Fail) Register(int cartId)
    {
        var attempt = attempts.AddOrUpdate(cartId, 1, (_, count) => count + 1);
        return (attempt, attempt <= FailFirstAttempts);
    }
}
