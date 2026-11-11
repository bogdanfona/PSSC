namespace Laborator8_PSSC.Events;

// How handling a message ended; the listener settles the message accordingly.
public enum EventProcessingResult
{
    // Handled (or already handled before): the message is removed.
    Completed,

    // Transient failure (a dependency is down): the message is delivered again later.
    Retry,

    // The message can never be handled (invalid data): it goes straight to the dead-letter queue.
    Failed,
}
