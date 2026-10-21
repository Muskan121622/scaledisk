using ScaleDesk.Domain.Common;

namespace ScaleDesk.Domain.Entities;

/// <summary>
/// A domain event persisted in the SAME transaction as the aggregate that raised it (outbox
/// pattern, spec §31). A separate dispatcher later publishes it to Kafka and marks it processed,
/// guaranteeing "committed in DB" and "event published" can never diverge — at-least-once.
/// </summary>
public class OutboxMessage : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    /// <summary>Topic / routing key, e.g. "ticket.created".</summary>
    public string EventType { get; set; } = default!;

    /// <summary>Serialized event payload (jsonb).</summary>
    public string Payload { get; set; } = default!;

    public DateTime OccurredAt { get; set; }

    /// <summary>
    /// W3C traceparent ("00-traceid-spanid-flags") captured from the ambient activity when the event
    /// was raised. Persisted on the row (not just held in memory) so the outbox dispatcher can attach it
    /// as a Kafka header seconds later — after the originating request has finished — letting the
    /// consumer span join the SAME distributed trace as the HTTP request (Stage 5B, spec §41).
    /// </summary>
    public string? TraceParent { get; set; }

    /// <summary>W3C tracestate carried alongside TraceParent for multi-vendor trace propagation.</summary>
    public string? TraceState { get; set; }

    public DateTime? ProcessedAt { get; set; }

    public int Attempts { get; set; }

    public string? LastError { get; set; }

    public bool IsPending => ProcessedAt is null;
}
