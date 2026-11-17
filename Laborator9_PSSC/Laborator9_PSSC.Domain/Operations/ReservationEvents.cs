using Laborator9_PSSC.Domain.Events;
using Laborator9_PSSC.Domain.States;

namespace Laborator9_PSSC.Domain.Operations;

public static class ReservationEvents
{
    extension(SpaceReservation.Placed reservation)
    {
        /// <summary>The success event, straight from the final state: failures never reach this point.</summary>
        public SpaceReservedEvent ToEvent() => new(
            reservation.Code.Value,
            reservation.Student.Value,
            reservation.Space.Value,
            reservation.Slot.Date,
            reservation.Slot.Start,
            reservation.Slot.End,
            reservation.Attendees.Value,
            reservation.Approval is ReservationApproval.AutoConfirmed ? ReservationStatus.Confirmed : ReservationStatus.AwaitingApproval,
            reservation.Approval is ReservationApproval.AutoConfirmed confirmed ? confirmed.RemindAt : null,
            reservation.PlacedAt);
    }
}
