namespace Laborator8_PSSC.Domain.Tests;

// A clock that always shows the same moment, so PlacedAt is predictable in tests.
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}
