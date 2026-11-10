using Laborator7_PSSC.Functional;
using Laborator7_PSSC.Domain.Models;

namespace Laborator7_PSSC.Domain.Repositories;

// Port: saves the workflow's result. Implemented in Data/Repositories.
public interface IOrdersRepository
{
    // Upserts the customer by code, inserts the order and takes the quantities out of stock, all or nothing.
    // Fails with InsufficientStock if the stock dropped after the workflow checked it.
    Task<Result<OrderId, PlaceOrderError>> SaveAsync(OrderPlacedEvent placed, CancellationToken cancellationToken);
}
