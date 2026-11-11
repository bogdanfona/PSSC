using Laborator8_PSSC.Events;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Laborator8_PSSC.Shipping.EventProcessor;

// Starts the listener when the host starts and stops it gracefully on shutdown (Ctrl+C).
internal sealed class Worker(IEventListener listener, IOptions<EventProcessorOptions> options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var o = options.Value;
        await listener.StartAsync(new TopicName(o.TopicName), new SubscriptionName(o.SubscriptionName), stoppingToken);

        // The listener is push-based: messages arrive on their own, so there is nothing to do but wait for shutdown.
        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken).ContinueWith(_ => { }, TaskScheduler.Default);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await listener.StopAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}
