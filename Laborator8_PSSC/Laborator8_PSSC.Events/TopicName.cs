namespace Laborator8_PSSC.Events;

// Value objects for entity names, so a topic is never confused with a subscription or any other string.
public readonly record struct TopicName
{
    public string Value { get; }

    public TopicName(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public override string ToString() => Value;
}

public readonly record struct SubscriptionName
{
    public string Value { get; }

    public SubscriptionName(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public override string ToString() => Value;
}
