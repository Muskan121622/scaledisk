namespace ScaleDesk.Infrastructure.Messaging;

/// <summary>
/// Kafka connection settings bound from the "Kafka" section. Disabled by default so local runs and
/// tests without a broker simply leave the outbox draining as a no-op; enabling it is one config flag.
/// </summary>
public sealed record KafkaOptions
{
    public const string SectionName = "Kafka";

    public bool Enabled { get; init; }

    public string BootstrapServers { get; init; } = "localhost:9092";

    /// <summary>Prefix for all topics, e.g. "scaledesk." => "scaledesk.ticket.created".</summary>
    public string TopicPrefix { get; init; } = "scaledesk.";

    /// <summary>Consumer group id for the inbound event processor. One group => competitive
    /// consumption across API replicas; each message lands on exactly one worker.</summary>
    public string ConsumerGroup { get; init; } = "scaledesk-notifications";

    /// <summary>Max outbox rows published per poll cycle.</summary>
    public int BatchSize { get; init; } = 50;

    public int PollIntervalMilliseconds { get; init; } = 1000;

    /// <summary>Consumer poll timeout; bounds how long a idle loop blocks before rechecking.
    /// Also the max interval between manual commits, so an unhandled message is retried sooner.</summary>
    public int ConsumerPollTimeoutMilliseconds { get; init; } = 1000;
}
