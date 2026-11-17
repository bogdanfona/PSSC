using Laborator9_PSSC.Domain.Repositories;
using Laborator9_PSSC.Domain.States;
using Laborator9_PSSC.Domain.ValueObjects;

namespace Laborator9_PSSC.ConsoleApp;

// In-memory adapters for the workflow's ports; stand-ins for the EF Core repositories of Lab 5.

internal sealed class InMemorySpaces(IReadOnlyDictionary<SpaceId, StudySpace> catalog) : IStudySpacesRepository
{
    public Task<IReadOnlyDictionary<SpaceId, StudySpace>> GetCatalogAsync(CancellationToken cancellationToken) => Task.FromResult(catalog);
}

internal sealed class InMemoryReservations(IEnumerable<ExistingReservation> initial) : IReservationsRepository
{
    private readonly List<ExistingReservation> active = [.. initial];

    public List<SpaceReservation.Placed> Saved { get; } = [];

    public Task<IReadOnlyList<ExistingReservation>> GetActiveAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ExistingReservation>>([.. active]);

    public Task SaveAsync(SpaceReservation.Placed reservation, CancellationToken cancellationToken)
    {
        Saved.Add(reservation);
        active.Add(new ExistingReservation(reservation.Space, reservation.Student, reservation.Slot));
        return Task.CompletedTask;
    }
}

internal sealed class InMemoryStanding(IReadOnlyDictionary<StudentId, DateTimeOffset> blocked) : IStudentStandingRepository
{
    public Task<IReadOnlyDictionary<StudentId, DateTimeOffset>> GetBlockedAsync(CancellationToken cancellationToken) => Task.FromResult(blocked);
}

internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();

    public override TimeZoneInfo LocalTimeZone { get; } = TimeZoneInfo.CreateCustomTimeZone("Campus", now.Offset, "Campus", "Campus");
}
