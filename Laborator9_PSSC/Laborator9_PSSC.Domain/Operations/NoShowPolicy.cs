using Laborator9_PSSC.Domain.ValueObjects;
using Laborator9_PSSC.Functional;

namespace Laborator9_PSSC.Domain.Operations;

/// <summary>
/// Rules of the Penalties context, as pure functions. They produce the "blocked until" data the reserve space
/// workflow receives as a dependency; the reservation context does not count no-shows itself.
/// </summary>
public static class NoShowPolicy
{
    public static readonly TimeSpan GracePeriod = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan CountingWindow = TimeSpan.FromDays(30);
    public static readonly TimeSpan BlockDuration = TimeSpan.FromDays(7);
    public const int NoShowsBeforeBlock = 3;

    /// <summary>More than 15 minutes after the start without a check-in, the reservation is cancelled as a no-show.</summary>
    public static bool IsNoShow(TimeSlot slot, bool checkedIn, DateTimeOffset now) =>
        !checkedIn && now.DateTime > slot.StartsAt + GracePeriod;

    /// <summary>3 no-shows within 30 days block the student for a week from the latest one; None if not blocked now.</summary>
    public static Option<DateTimeOffset> BlockedUntil(IReadOnlyList<DateTimeOffset> noShows, DateTimeOffset now)
    {
        var recent = noShows.Where(n => n <= now && now - n <= CountingWindow).OrderDescending().ToList();
        if (recent.Count < NoShowsBeforeBlock)
            return Option.None<DateTimeOffset>();

        var until = recent[0] + BlockDuration;
        return until > now ? Option.Some(until) : Option.None<DateTimeOffset>();
    }
}
