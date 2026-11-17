using Laborator9_PSSC.Domain.Errors;
using Laborator9_PSSC.Domain.States;
using Laborator9_PSSC.Domain.ValueObjects;
using Laborator9_PSSC.Functional;

namespace Laborator9_PSSC.Domain.Operations;

public static class ReservationRules
{
    /// <summary>Hours a student may hold per day: 2 h of individual spaces, 4 h of group rooms and labs.</summary>
    public static readonly TimeSpan IndividualDailyLimit = TimeSpan.FromHours(2);
    public static readonly TimeSpan GroupDailyLimit = TimeSpan.FromHours(4);

    extension(SpaceReservation.Validated reservation)
    {
        /// <summary>
        /// Applies the reservation rules. They are independent of each other, so every broken rule is reported.
        /// Dependencies are parameters: the catalog, the reservations already made, the penalty blocks and the clock.
        /// </summary>
        public Result<SpaceReservation.Checked, IReadOnlyList<ReservationRuleViolation>> CheckRules(
            IReadOnlyDictionary<SpaceId, StudySpace> spaces,
            IReadOnlyList<ExistingReservation> existing,
            IReadOnlyDictionary<StudentId, DateTimeOffset> blockedUntil,
            DateTimeOffset now)
        {
            // Validation already ensured the space exists, so this lookup cannot miss.
            var space = spaces[reservation.Space];
            var slot = reservation.Slot;
            var attendees = reservation.Attendees.Value;

            return Result.Check<SpaceReservation.Checked, ReservationRuleViolation>(
                new SpaceReservation.Checked(reservation.Student, space, slot, reservation.Attendees),
                slot.StartsAt <= now.DateTime ? new ReservationRuleViolation.StartsInPast(slot) : null,
                blockedUntil.TryGetValue(reservation.Student, out var until) && until > now
                    ? new ReservationRuleViolation.StudentBlocked(reservation.Student, until)
                    : null,
                // An individual space is reported by its own rule below, not twice (it also has capacity 1).
                reservation.Space.Type != SpaceType.Individual && attendees > space.Capacity.Value
                    ? new ReservationRuleViolation.OverCapacity(attendees, space.Capacity.Value)
                    : null,
                reservation.Space.Type switch
                {
                    SpaceType.Individual when attendees > 1 => new ReservationRuleViolation.IndividualSpaceForOnePerson(attendees),
                    SpaceType.Group when attendees < 2 => new ReservationRuleViolation.GroupSpaceNeedsGroup(attendees),
                    _ => null,
                },
                existing.FirstOrDefault(r => r.Space == reservation.Space && r.Slot.OverlapsWith(slot)) is { } taken
                    ? new ReservationRuleViolation.SlotTaken(taken.Space, taken.Slot)
                    : null,
                existing.FirstOrDefault(r => r.Student == reservation.Student && r.Space != reservation.Space && r.Slot.OverlapsWith(slot)) is { } own
                    ? new ReservationRuleViolation.StudentAlreadyBooked(own.Space, own.Slot)
                    : null,
                DailyLimit(reservation, existing));
        }
    }

    // Individual spaces count against the 2 h limit; group rooms and labs share the 4 h limit.
    private static ReservationRuleViolation.DailyLimitExceeded? DailyLimit(SpaceReservation.Validated reservation, IReadOnlyList<ExistingReservation> existing)
    {
        var individual = reservation.Space.Type == SpaceType.Individual;
        var limit = individual ? IndividualDailyLimit : GroupDailyLimit;
        var booked = existing
            .Where(r => r.Student == reservation.Student
                && r.Slot.Date == reservation.Slot.Date
                && (r.Space.Type == SpaceType.Individual) == individual)
            .Aggregate(TimeSpan.Zero, (total, r) => total + r.Slot.Duration);

        return booked + reservation.Slot.Duration > limit
            ? new ReservationRuleViolation.DailyLimitExceeded(reservation.Space.Type, booked, reservation.Slot.Duration, limit)
            : null;
    }
}
