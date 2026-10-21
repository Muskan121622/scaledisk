using ScaleDesk.Domain.Common;

namespace ScaleDesk.Application.Abstractions;

/// <summary>
/// Per-request buffer of domain events raised by services during a unit of work. The persistence
/// layer drains it inside <c>SaveChangesAsync</c> and writes an <c>OutboxMessage</c> per event in
/// the SAME transaction, so publishing is atomic with the data change (outbox pattern).
/// Services depend on this abstraction and never touch Kafka or the outbox table directly.
/// </summary>
public interface IEventCollector
{
    void Raise(IDomainEvent domainEvent);

    IReadOnlyList<IDomainEvent> Pending { get; }

    void Clear();
}
