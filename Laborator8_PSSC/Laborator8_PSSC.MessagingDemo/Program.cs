using System.Text.Json;
using Azure.Messaging.ServiceBus;

// Task 2, with the Service Bus SDK used directly (no abstractions), as in the Microsoft quickstarts:
//   queue-send [n]                    sends n orders to the "orders" queue
//   queue-receive                     receives them; each message goes to exactly one receiver (1-to-1)
//   topic-send [n]                    publishes n messages on the "order-placed" topic
//   topic-receive <subscription>      receives from "invoicing" or "shipping"; every subscription gets its own copy (1-to-many)
//   dead-letters <subscription>       shows the messages moved to the dead-letter queue of a subscription
// Run each receiver in its own terminal, then the sender in another one.

const string Queue = "orders";
const string Topic = "order-placed";

// The emulator's connection string is a public constant, the same for everyone. A real namespace's connection
// string never goes in code or appsettings.json: use the ConnectionStrings__ServiceBus environment variable.
var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__ServiceBus")
    ?? "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

using var stop = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Cancel(); };

await using var client = new ServiceBusClient(connectionString);

switch (args)
{
    case ["queue-send", .. var rest]:
        await SendAsync(client.CreateSender(Queue), Count(rest), "coada " + Queue);
        break;
    case ["queue-receive"]:
        await ReceiveAsync(client.CreateProcessor(Queue), "coada " + Queue, stop.Token);
        break;
    case ["topic-send", .. var rest]:
        await SendAsync(client.CreateSender(Topic), Count(rest), "topicul " + Topic);
        break;
    case ["topic-receive", var subscription]:
        await ReceiveAsync(client.CreateProcessor(Topic, subscription), $"{Topic}/{subscription}", stop.Token);
        break;
    case ["dead-letters", var subscription]:
        await ShowDeadLettersAsync(client.CreateReceiver(Topic, subscription, new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter }), subscription);
        break;
    default:
        Console.WriteLine("Utilizare: queue-send [n] | queue-receive | topic-send [n] | topic-receive <invoicing|shipping> | dead-letters <invoicing|shipping>");
        return 1;
}
return 0;

static int Count(string[] rest) => rest is [var n] && int.TryParse(n, out var count) && count > 0 ? count : 3;

// A batch is sent in one call; TryAddMessage says when the batch is full.
static async Task SendAsync(ServiceBusSender sender, int count, string target)
{
    await using var _ = sender;
    using var batch = await sender.CreateMessageBatchAsync();
    for (var i = 1; i <= count; i++)
    {
        var order = new DemoOrder(i, $"Client {i}", Math.Round(10m * i + 0.5m, 2), DateTimeOffset.Now);
        var message = new ServiceBusMessage(JsonSerializer.SerializeToUtf8Bytes(order, JsonSerializerOptions.Web))
        {
            ContentType = "application/json",
            Subject = "demo.order",
            MessageId = Guid.NewGuid().ToString(),
        };
        if (!batch.TryAddMessage(message))
            throw new InvalidOperationException($"Mesajul {i} nu incape in lot.");
    }
    await sender.SendMessagesAsync(batch);
    Console.WriteLine($"Am trimis {count} mesaje pe {target}.");
}

// The processor pushes messages to the handler. A message is completed only after it was handled;
// if the handler throws, it is abandoned and delivered again.
static async Task ReceiveAsync(ServiceBusProcessor processor, string source, CancellationToken stop)
{
    await using var _ = processor;
    processor.ProcessMessageAsync += async args =>
    {
        var m = args.Message;
        Console.WriteLine($"[{source}] {m.Subject ?? "-"} | livrarea {m.DeliveryCount} | {m.Body}");
        await args.CompleteMessageAsync(m, args.CancellationToken);
    };
    processor.ProcessErrorAsync += args =>
    {
        Console.WriteLine($"[{source}] eroare: {args.Exception.Message}");
        return Task.CompletedTask;
    };

    await processor.StartProcessingAsync(stop);
    Console.WriteLine($"Ascult pe {source}. Ctrl+C pentru oprire.");
    try
    {
        await Task.Delay(Timeout.Infinite, stop);
    }
    catch (OperationCanceledException)
    {
    }
    await processor.StopProcessingAsync(CancellationToken.None);
}

// Peek only: the messages stay in the dead-letter queue.
static async Task ShowDeadLettersAsync(ServiceBusReceiver receiver, string subscription)
{
    await using var _ = receiver;
    var messages = await receiver.PeekMessagesAsync(maxMessages: 50);
    Console.WriteLine($"{messages.Count} mesaje moarte in {subscription}:");
    foreach (var m in messages)
        Console.WriteLine($"- {m.MessageId} | {m.Subject ?? "-"} | motiv: {m.DeadLetterReason} | {m.DeadLetterErrorDescription}");
}

internal sealed record DemoOrder(int Number, string Customer, decimal Total, DateTimeOffset CreatedAt);
