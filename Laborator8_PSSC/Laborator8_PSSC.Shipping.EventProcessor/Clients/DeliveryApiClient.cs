using System.Net.Http.Json;
using Laborator8_PSSC.Contracts.Deliveries;

namespace Laborator8_PSSC.Shipping.EventProcessor.Clients;

// Typed client for the delivery API. The retry policy is attached where it is registered (ServiceCollectionExtensions),
// so this class only knows the route and the contract.
public sealed class DeliveryApiClient(HttpClient http)
{
    // Throws HttpRequestException (or a timeout) once the retries are used up.
    public async Task<DeliveryConfirmation> ScheduleAsync(DeliveryRequest request, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync("deliveries", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DeliveryConfirmation>(cancellationToken)
            ?? throw new InvalidOperationException("Raspuns gol de la API-ul de livrari.");
    }
}
