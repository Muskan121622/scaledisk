using ScaleDesk.Application.Abstractions;
using ScaleDesk.Domain.Common;

namespace ScaleDesk.Infrastructure.Messaging;

/// <summary>Scoped, per-request implementation of <see cref="IEventCollector"/>.</summary>
public sealed class DomainEventCollector : IEventCollector
{
    private readonly List<IDomainEvent> _pending = new();

    public void Raise(IDomainEvent domainEvent) => _pending.Add(domainEvent);

    public IReadOnlyList<IDomainEvent> Pending => _pending;

    public void Clear() => _pending.Clear();
}
