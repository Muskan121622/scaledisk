using System.Diagnostics;
using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using ScaleDesk.Infrastructure.Diagnostics;

namespace ScaleDesk.Infrastructure.Messaging;

/// <summary>
/// Thin wrapper over a Confluent.Kafka producer. Idempotent producer + leader acks give us
/// effectively-once *delivery into Kafka*; combined with the outbox, publishing is at-least-once
/// end to end, so consumers must be idempotent (spec §32).
/// </summary>
public sealed class KafkaEventPublisher : IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly KafkaOptions _options;
    private readonly ILogger _logger;

    public KafkaEventPublisher(KafkaOptions options, ILogger logger)
    {
        _options = options;
        _logger = logger;

        var config = new ProducerConfig
        {
            BootstrapServers = options.BootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true,
            MessageMaxBytes = 5_000_000
        };

        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    public async Task PublishAsync(
        string eventType, string partitionKey, string payload,
        string? traceParent, string? traceState, string? eventId, CancellationToken ct = default)
    {
        var topic = _options.TopicPrefix + eventType;

        // Restore the originating request's trace context (captured on the outbox row) and open a
        // producer span as its child, then write it back out as W3C headers so the consumer continues
        // the SAME trace across the process/broker boundary.
        ActivityContext parentContext = default; var hasParent = traceParent is not null && ActivityContext.TryParse(traceParent, traceState, out parentContext);

        // Always carry the stable business event id (the outbox row id) as a header so the consumer can
        // dedup on it. Headers are created lazily: a record with neither trace context nor event id
        // stays header-free.
        Headers? headers = null;
        if (hasParent || eventId is not null)
        {
            headers = new Headers();
            if (hasParent)
            {
                headers.Add(new Header("traceparent", Encoding.UTF8.GetBytes(traceParent!)));
                if (!string.IsNullOrEmpty(traceState))
                    headers.Add(new Header("tracestate", Encoding.UTF8.GetBytes(traceState!)));
            }
            if (eventId is not null)
                headers.Add(new Header("x-event-id", Encoding.UTF8.GetBytes(eventId)));
        }

        using var activity = ScaledeskActivitySource.Source.StartActivity(
            "outbox publish", ActivityKind.Producer, hasParent ? parentContext : default);
        if (activity is not null)
        {
            activity.SetTag(ScaledeskActivitySource.AttrSystem, ScaledeskActivitySource.SystemKafka);
            activity.SetTag(ScaledeskActivitySource.AttrDestination, topic);
            activity.SetTag(ScaledeskActivitySource.AttrOperation, "publish");
            activity.SetTag("messaging.event.type", eventType);
        }

        var result = await _producer.ProduceAsync(topic,
            new Message<string, string> { Key = partitionKey, Value = payload, Headers = headers }, ct);

        activity?.SetTag(ScaledeskActivitySource.AttrPartition, result.Partition.Value);
        activity?.SetTag(ScaledeskActivitySource.AttrOffset, result.Offset.Value);

        _logger.LogDebug("Published {EventType} to {Topic} partition {Partition} offset {Offset}",
            eventType, topic, result.Partition.Value, result.Offset.Value);
    }

    public void Dispose()
    {
        try { _producer.Flush(TimeSpan.FromSeconds(5)); }
        catch (Exception ex) { _logger.LogWarning(ex, "Kafka flush on dispose failed"); }
        _producer.Dispose();
    }
}
