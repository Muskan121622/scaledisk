using System.Diagnostics;

namespace ScaleDesk.Infrastructure.Diagnostics;

/// <summary>
/// Single <see cref="ActivitySource"/> for the asynchronous event pipeline. The outbox dispatcher starts a
/// producer activity here before publishing to Kafka, and the consumer starts a linked consumer activity
/// when it processes a record. Because OpenTelemetry listens to this source, the spans flow to the same
/// OTLP exporter as the automatic HTTP/EF instrumentation — so a ticket request, its outbox row, the Kafka
/// publish and the downstream notification/automation handling all render as one trace (spec §41, Stage 5B).
/// </summary>
public static class ScaledeskActivitySource
{
    /// <summary>Source name; must be registered via AddSource(...) on the tracer provider.</summary>
    public const string Name = "scaledesk";

    public static readonly ActivitySource Source = new(Name, "1.0.0");

    // SemConv messaging attributes (stable subset) reused across producer/consumer spans.
    public const string SystemKafka = "kafka";
    public const string AttrSystem = "messaging.system";
    public const string AttrDestination = "messaging.destination.name";
    public const string AttrOperation = "messaging.operation";
    public const string AttrPartition = "messaging.kafka.source.partition";
    public const string AttrOffset = "messaging.kafka.message.offset";
}
