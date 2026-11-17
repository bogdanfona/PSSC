using Laborator9_PSSC.Domain.Commands;
using Laborator9_PSSC.Domain.Errors;
using Laborator9_PSSC.Domain.Events;
using Laborator9_PSSC.Domain.Operations;
using Laborator9_PSSC.Domain.Repositories;
using Laborator9_PSSC.Domain.States;
using Laborator9_PSSC.Domain.ValueObjects;
using Laborator9_PSSC.Functional;

namespace Laborator9_PSSC.Domain.Workflows;

/// <summary>
/// Reserves a study space. Impure -> pure -> impure: <see cref="ExecuteAsync"/> loads what the rules need through the ports,
/// <see cref="Reserve"/> decides with pure functions, and the placed reservation is saved only on the success branch.
/// </summary>
public sealed class ReserveSpaceWorkflow(
    IStudySpacesRepository spaces,
    IReservationsRepository reservations,
    IStudentStandingRepository standing,
    TimeProvider clock)
{
    public async Task<Result<SpaceReservedEvent, ReserveSpaceError>> ExecuteAsync(ReserveSpaceCommand command, CancellationToken cancellationToken)
    {
        var catalog = await spaces.GetCatalogAsync(cancellationToken);
        var existing = await reservations.GetActiveAsync(cancellationToken);
        var blocked = await standing.GetBlockedAsync(cancellationToken);

        return await Reserve(command, catalog, existing, blocked, clock.GetLocalNow())
            .TapAsync(placed => reservations.SaveAsync(placed, cancellationToken))
            .Map(placed => placed.ToEvent());
    }

    /// <summary>The pure core, mapped to the success event. No business logic here: only composition.</summary>
    public static Result<SpaceReservedEvent, ReserveSpaceError> Publish(
        ReserveSpaceCommand command,
        IReadOnlyDictionary<SpaceId, StudySpace> catalog,
        IReadOnlyList<ExistingReservation> existing,
        IReadOnlyDictionary<StudentId, DateTimeOffset> blockedUntil,
        DateTimeOffset now) =>
        Reserve(command, catalog, existing, blockedUntil, now).Map(placed => placed.ToEvent());

    /// <summary>
    /// Railway: a validation error skips every later step, and so does a broken rule. Validation and rules are two
    /// separate tracks on purpose: a rule (capacity, conflicts) only makes sense once every field is well formed.
    /// </summary>
    public static Result<SpaceReservation.Placed, ReserveSpaceError> Reserve(
        ReserveSpaceCommand command,
        IReadOnlyDictionary<SpaceId, StudySpace> catalog,
        IReadOnlyList<ExistingReservation> existing,
        IReadOnlyDictionary<StudentId, DateTimeOffset> blockedUntil,
        DateTimeOffset now) =>
        new SpaceReservation.Unvalidated(command.StudentId, command.SpaceId, command.Date, command.Start, command.End, command.Attendees)
            .Validate(catalog)
            .MapError(errors => (ReserveSpaceError)new ReserveSpaceError.Validation(errors))
            .Bind(validated => validated
                .CheckRules(catalog, existing, blockedUntil, now)
                .MapError(violations => (ReserveSpaceError)new ReserveSpaceError.RulesViolated(violations)))
            .Map(checkedReservation => checkedReservation.Place(now));
}
