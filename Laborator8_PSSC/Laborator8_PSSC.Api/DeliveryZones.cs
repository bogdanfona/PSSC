using Laborator8_PSSC.Domain.Models;

namespace Laborator8_PSSC.Api;

// Stand-in for a delivery service; the database has no table for it.
public static class DeliveryZones
{
    public static readonly IReadOnlySet<string> Cities =
        new HashSet<string>(["Timisoara", "Arad", "Cluj-Napoca", "Bucuresti", "Iasi"], StringComparer.OrdinalIgnoreCase);

    public static bool IsDeliverable(Address address) => Cities.Contains(address.City);
}
