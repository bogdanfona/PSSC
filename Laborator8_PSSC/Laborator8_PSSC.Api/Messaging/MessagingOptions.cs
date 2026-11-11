using System.ComponentModel.DataAnnotations;

namespace Laborator8_PSSC.Api.Messaging;

// Where this API publishes its events; bound from the "Messaging" section and checked at startup.
public sealed class MessagingOptions
{
    public const string SectionName = "Messaging";

    [Required(AllowEmptyStrings = false)]
    public string OrderPlacedTopic { get; set; } = string.Empty;
}
