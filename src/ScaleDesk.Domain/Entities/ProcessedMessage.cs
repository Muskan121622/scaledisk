using ScaleDesk.Domain.Common;

namespace ScaleDesk.Domain.Entities;

/// <summary>
/// Idempotent consumer guard (spec §32). Outbox → Kafka is at-least-once; redeliveries happen on
/// consumer restart or rebalance. Before processing a message, we check whether (topic, partition,
/// offset) is already here; if so we skip. Committed in the SAME transaction as the projection
/// (notification insert), so we never process an event twice without recording it.
/// </summary>
public class ProcessedMessage : BaseEntity
{
    /// <summary>Unique composite key: "topic:partition:offset".</summary>
    public string DeduplicationKey { get; set; } = default!;

    public string Topic { get; set; } = default!;
    public int Partition { get; set; }
    public long Offset { get; set; }

    public DateTime ProcessedAt { get; set; }
}
