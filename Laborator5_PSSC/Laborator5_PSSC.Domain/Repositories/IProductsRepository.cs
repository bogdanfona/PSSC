using Laborator5_PSSC.Domain.Models;

namespace Laborator5_PSSC.Domain.Repositories;

// Port: what the domain needs to read about products. Implemented in Data/Repositories.
public interface IProductsRepository
{
    // Codes that are not in the database are simply missing from the result.
    Task<ProductCatalog> GetByCodesAsync(IReadOnlyCollection<ProductCode> codes, CancellationToken cancellationToken);
}
