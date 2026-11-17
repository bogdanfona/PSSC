using Laborator9_PSSC.Domain.States;
using Laborator9_PSSC.Domain.ValueObjects;

namespace Laborator9_PSSC.Domain.Operations;

public static class ReservationPlacement
{
    /// <summary>Reservations longer than this wait for a librarian; shorter ones are confirmed automatically.</summary>
    public static readonly TimeSpan AutoConfirmLimit = TimeSpan.FromHours(2);

    /// <summary>The reminder goes out this long before the start.</summary>
    public static readonly TimeSpan ReminderLead = TimeSpan.FromMinutes(30);

    extension(SpaceReservation.Checked reservation)
    {
        /// <summary>
        /// Total: every checked reservation can be placed. Labs (expensive equipment) and intervals over 2 h need approval;
        /// everything else is confirmed with a reminder 30 minutes before the start.
        /// </summary>
        public SpaceReservation.Placed Place(DateTimeOffset now) => new(
            ReservationCode.For(reservation.Space.Id, reservation.Slot),
            reservation.Student,
            reservation.Space.Id,
            reservation.Slot,
            reservation.Attendees,
            Decide(reservation),
            now);
    }

    private static ReservationApproval Decide(SpaceReservation.Checked reservation) => reservation switch
    {
        { Space.Id.Type: SpaceType.Lab } => new ReservationApproval.AwaitingApproval("Laboratoarele se rezerva doar cu aprobarea bibliotecarului."),
        { Slot.Duration: var duration } when duration > AutoConfirmLimit =>
            new ReservationApproval.AwaitingApproval($"Intervalele de peste {AutoConfirmLimit.TotalHours:0} h necesita aprobare."),
        _ => new ReservationApproval.AutoConfirmed(reservation.Slot.StartsAt - ReminderLead),
    };
}
