using Laborator9_PSSC.Domain.States;
using Laborator9_PSSC.Domain.ValueObjects;

namespace Laborator9_PSSC.Domain.Repositories;

// Ports of the reservation context. In this lab they are implemented in memory (console app, tests);
// with Lab 5's approach they would be EF Core adapters.

public interface IStudySpacesRepository
{
    Task<IReadOnlyDictionary<SpaceId, StudySpace>> GetCatalogAsync(CancellationToken cancellationToken);
}

public interface IReservationsRepository
{
    Task<IReadOnlyList<ExistingReservation>> GetActiveAsync(CancellationToken cancellationToken);

    Task SaveAsync(SpaceReservation.Placed reservation, CancellationToken cancellationToken);
}

/// <summary>Provided by the Penalties context: who is blocked, and until when.</summary>
public interface IStudentStandingRepository
{
    Task<IReadOnlyDictionary<StudentId, DateTimeOffset>> GetBlockedAsync(CancellationToken cancellationToken);
}
