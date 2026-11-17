using Laborator9_PSSC.Domain.Errors;
using Laborator9_PSSC.Domain.States;
using Laborator9_PSSC.Domain.ValueObjects;
using Laborator9_PSSC.Functional;

namespace Laborator9_PSSC.Domain.Operations;

public static class ReservationValidation
{
    extension(SpaceReservation.Unvalidated reservation)
    {
        /// <summary>
        /// Parses every field and checks that the space is in the catalog. The four fields are independent, so
        /// <see cref="Result.Combine{T1, T2, T3, T4, TFailure, TResult}"/> reports all their errors at once.
        /// </summary>
        // C# 14 gotcha: MapError<ValidationError>(...) with an explicit type argument does not resolve on an extension
        // member, so each lambda casts its result to ValidationError and inference does the rest.
        public Result<SpaceReservation.Validated, IReadOnlyList<ValidationError>> Validate(IReadOnlyDictionary<SpaceId, StudySpace> spaces) =>
            Result.Combine(
                StudentId.Parse(reservation.StudentId).MapError(e => (ValidationError)new ValidationError.InvalidStudentId(e)),
                SpaceId.Parse(reservation.SpaceId)
                    .MapError(e => (ValidationError)new ValidationError.InvalidSpaceId(e))
                    .Ensure(spaces.ContainsKey, space => new ValidationError.UnknownSpace(space)),
                TimeSlot.Parse(reservation.Date, reservation.Start, reservation.End)
                    .MapError(e => (ValidationError)new ValidationError.InvalidTimeSlot(e)),
                Headcount.Parse(reservation.Attendees).MapError(e => (ValidationError)new ValidationError.InvalidAttendees(e)),
                (student, space, slot, attendees) => new SpaceReservation.Validated(student, space, slot, attendees));
    }
}
