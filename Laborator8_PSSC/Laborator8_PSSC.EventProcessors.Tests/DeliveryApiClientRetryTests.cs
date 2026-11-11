using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Laborator8_PSSC.Shipping.EventProcessor.Clients;
using Laborator8_PSSC.Contracts.Deliveries;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Laborator8_PSSC.EventProcessors.Tests;

// The client is registered exactly as in Program.cs (AddDeliveryApiClient), but its requests go to a fake
// handler instead of the network, so the tests count how many attempts the retry policy makes.
public class DeliveryApiClientRetryTests
{
    private static readonly DeliveryRequest Request = new(
        7, "C-100", "Ana Pop", "Str. Lalelelor 5, Timisoara 300001",
        [new DeliveryItem("MER-0001", "Mere", 3)], 12.60m, new DateTime(2026, 11, 10, 12, 0, 0));

    private static readonly DeliveryConfirmation Confirmation = new(
        7, "AWB-000007", Request.DeliveryAddress, new DateOnly(2026, 11, 12), new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task ScheduleAsync_ServiceUnavailableTwice_SucceedsOnThirdAttempt()
    {
        var handler = new ScriptedHandler(HttpStatusCode.ServiceUnavailable, HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK);

        var delivery = await CreateClient(handler).ScheduleAsync(Request, CancellationToken.None);

        Assert.Equal("AWB-000007", delivery.TrackingNumber);
        Assert.Equal(3, handler.Attempts);
    }

    [Fact]
    public async Task ScheduleAsync_AlwaysFailing_RetriesThreeTimesThenThrows()
    {
        var handler = new ScriptedHandler(HttpStatusCode.InternalServerError);

        await Assert.ThrowsAsync<HttpRequestException>(() => CreateClient(handler).ScheduleAsync(Request, CancellationToken.None));

        // The first attempt plus 3 retries.
        Assert.Equal(4, handler.Attempts);
    }

    [Theory]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.BadGateway)]
    public async Task ScheduleAsync_TransientStatus_IsRetried(HttpStatusCode status)
    {
        var handler = new ScriptedHandler(status, HttpStatusCode.OK);

        await CreateClient(handler).ScheduleAsync(Request, CancellationToken.None);

        Assert.Equal(2, handler.Attempts);
    }

    [Fact]
    public async Task ScheduleAsync_NetworkError_IsRetried()
    {
        var handler = new ScriptedHandler(null, HttpStatusCode.OK);

        await CreateClient(handler).ScheduleAsync(Request, CancellationToken.None);

        Assert.Equal(2, handler.Attempts);
    }

    [Fact]
    public async Task ScheduleAsync_BadRequest_IsNotRetried()
    {
        var handler = new ScriptedHandler(HttpStatusCode.BadRequest);

        await Assert.ThrowsAsync<HttpRequestException>(() => CreateClient(handler).ScheduleAsync(Request, CancellationToken.None));

        Assert.Equal(1, handler.Attempts);
    }

    [Fact]
    public async Task ScheduleAsync_SendsTheCartToTheDeliveriesRoute()
    {
        var handler = new ScriptedHandler(HttpStatusCode.OK);

        await CreateClient(handler).ScheduleAsync(Request, CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal(new Uri("https://delivery.test/deliveries"), handler.LastRequest.RequestUri);
        Assert.Equal(Request.CartId, handler.LastBody!.CartId);
    }

    // Shortest allowed base delay, so the exponential waits stay small (about 50, 100, 200 ms).
    internal static DeliveryApiClient CreateClient(ScriptedHandler handler)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DeliveryApi:BaseAddress"] = "https://delivery.test/",
                ["DeliveryApi:Retry:MaxRetryAttempts"] = "3",
                ["DeliveryApi:Retry:BaseDelayMilliseconds"] = "50",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddDeliveryApiClient(configuration);
        services.AddHttpClient<DeliveryApiClient>().ConfigurePrimaryHttpMessageHandler(() => handler);
        return services.BuildServiceProvider().GetRequiredService<DeliveryApiClient>();
    }

    // Answers with the given statuses in order and repeats the last one; null throws a network error.
    internal sealed class ScriptedHandler(params HttpStatusCode?[] script) : HttpMessageHandler
    {
        public int Attempts { get; private set; }
        public HttpRequestMessage? LastRequest { get; private set; }
        public DeliveryRequest? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var status = script[Math.Min(Attempts, script.Length - 1)];
            Attempts++;
            LastRequest = request;
            // Read as text: it buffers the body once, so every retry of the same request can read it again.
            LastBody = JsonSerializer.Deserialize<DeliveryRequest>(
                await request.Content!.ReadAsStringAsync(cancellationToken), JsonSerializerOptions.Web);

            if (status is null)
                throw new HttpRequestException("Connection refused (simulated).");

            return status == HttpStatusCode.OK
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Confirmation) }
                : new HttpResponseMessage(status.Value);
        }
    }
}
