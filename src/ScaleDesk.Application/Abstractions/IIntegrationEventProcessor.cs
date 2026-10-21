namespace ScaleDesk.Application.Abstractions;

/// <summary>
/// A single message read off Kafka, carrying just enough routing metadata for idempotency.
/// The Application layer defines this so the consumer (Infrastructure) depends on an abstraction,
/// and the event-handling logic is testable without a live broker.
/// </summary>
public sealed record ConsumedIntegrationEvent(
    string Topic,
    int Partition,
    long Offset,
    string EventType,
    string PayloadJson,
    string? EventId = null)
{
    /// <summary>
    /// Idempotency key (spec §32). When the producer tagged the record with a stable business event id
    /// (the originating outbox row id, carried in the x-event-id header) we dedup on THAT, so a logical
    /// event is processed exactly once even if it is re-published to a different offset (outbox replay,
    /// manual re-emit, topic copy). Without a business id we fall back to the (topic, partition, offset)
    /// position, which still guards the ordinary at-least-once redelivery of the same record.
    /// </summary>
    public string DeduplicationKey => EventId is not null ? $"evt:{EventId}" : $"{Topic}:{Partition}:{Offset}";
}

/// <summary>
/// Orchestrates handling of one integration event: dedup guard + a single transaction spanning all
/// registered <see cref="IIntegrationEventHandler"/>s, so projections commit atomically with the
/// ProcessedMessage marker (spec §32). The consumer resolves this one contract per message.
/// </summary>
public interface IIntegrationEventProcessor
{
    Task ProcessAsync(ConsumedIntegrationEvent message, CancellationToken ct = default);
}

/// <summary>
/// A pluggable reaction to an integration event — notifications, automation, (future) analytics.
/// Handlers write their projections through the shared, ambient DbContext but MUST NOT call
/// SaveChanges or manage dedup; the orchestrator owns the transaction. To deliver a real-time effect
/// safely (only after commit), register it on the supplied <see cref="IntegrationEventDispatch"/>
/// rather than pushing inline. Handlers must be idempotent: at-least-once delivery means the same
/// event can arrive more than once, and the orchestrator's dedup check is the only guard.
/// </summary>
public interface IIntegrationEventHandler
{
    Task HandleAsync(ConsumedIntegrationEvent message, IntegrationEventDispatch dispatch, CancellationToken ct = default);
}

/// <summary>
/// Collects deferred real-time effects produced by handlers during a single message's processing.
/// Each effect is a closure over the already-built payload; the orchestrator runs them strictly AFTER
/// the transaction commits, so no client is ever told about a projection that rolled back (§21).
/// </summary>
public sealed class IntegrationEventDispatch
{
    private readonly List<Func<IRealtimeNotifier, CancellationToken, Task>> _effects = [];

    public void AfterCommit(Func<IRealtimeNotifier, CancellationToken, Task> effect) => _effects.Add(effect);

    public IReadOnlyList<Func<IRealtimeNotifier, CancellationToken, Task>> Effects => _effects;
}
