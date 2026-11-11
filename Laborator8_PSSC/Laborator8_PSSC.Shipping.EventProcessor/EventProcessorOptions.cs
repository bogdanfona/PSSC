using System.ComponentModel.DataAnnotations;

namespace Laborator8_PSSC.Shipping.EventProcessor;

// Which topic and subscription this worker listens to; bound from the "EventProcessor" section.
public sealed class EventProcessorOptions
{
    public const string SectionName = "EventProcessor";

    [Required(AllowEmptyStrings = false)]
    public string TopicName { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string SubscriptionName { get; set; } = string.Empty;
}
