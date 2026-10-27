using Laborator2_PSSC.Domain.Entities;
using Laborator2_PSSC.Domain.ValueObjects;

namespace Laborator2_PSSC.Domain.States;

public interface ICart
{
    CartId Id { get; }
    Customer Customer { get; }
}
