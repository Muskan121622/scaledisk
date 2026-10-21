namespace ScaleDesk.Domain.Common;

/// <summary>
/// A fact that happened in the domain, destined to become an integration event on Kafka via the
/// outbox. Events carry only identifiers and coarse attributes — never sensitive ticket content
/// (spec §37: structured logs / events must not dump customer data).
/// </summary>
public interface IDomainEvent
{
    /// <summary>Stable routing key / topic name, e.g. "ticket.created".</summary>
    string EventType { get; }

    /// <summary>Tenant the event belongs to; used to keep the outbox row tenant-scoped.</summary>
    Guid TenantId { get; }
}
