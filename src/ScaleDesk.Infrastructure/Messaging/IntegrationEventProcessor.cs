using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ScaleDesk.Application.Abstractions;
using ScaleDesk.Domain.Entities;
using ScaleDesk.Infrastructure.Persistence;

namespace ScaleDesk.Infrastructure.Messaging;

/// <summary>
/// The single <see cref="IIntegrationEventProcessor"/> the consumer resolves per message. It owns the
/// two cross-cutting guarantees every projection depends on and must not be duplicated by handlers:
///   1. idempotency — a (topic, partition, offset) already in <see cref="ProcessedMessage"/> is skipped
///      outright, so at-least-once redelivery never double-applies (spec §32);
///   2. atomicity  — all registered <see cref="IIntegrationEventHandler"/>s stage their changes on the
///      shared context, then ONE transaction commits them together with the dedup marker.
/// After the commit it does a guarded cross-cutting board broadcast for ticket events and flushes the
/// handlers' deferred real-time effects (spec §21); a push failure can never unwind durable state.
/// </summary>
public sealed class IntegrationEventProcessor(
    ScaleDeskDbContext db,
    IUnitOfWork uow,
    IClock clock,
    IRealtimeNotifier realtime,
    IEnumerable<IIntegrationEventHandler> handlers,
    ILogger<IntegrationEventProcessor> logger) : IIntegrationEventProcessor
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task ProcessAsync(ConsumedIntegrationEvent message, CancellationToken ct = default)
    {
        if (await db.ProcessedMessages.AnyAsync(p => p.DeduplicationKey == message.DeduplicationKey, ct))
        {
            logger.LogDebug("Skipping duplicate event {Key} ({EventType}).", message.DeduplicationKey, message.EventType);
            return;
        }

        var dispatch = new IntegrationEventDispatch();
        foreach (var handler in handlers)
            await handler.HandleAsync(message, dispatch, ct);

        // The marker is committed in the SAME transaction as every handler's projections.
        db.ProcessedMessages.Add(new ProcessedMessage
        {
            Topic = message.Topic,
            Partition = message.Partition,
            Offset = message.Offset,
            DeduplicationKey = message.DeduplicationKey,
            ProcessedAt = clock.UtcNow,
            CreatedAt = clock.UtcNow
        });

        await uow.SaveChangesAsync(ct);

        // Post-commit fan-out only. Guarded so a flaky realtime channel can never fail an already
        // durable projection; REST stays the source of truth on reconnect.
        try
        {
            await BroadcastTicketChangeAsync(message, ct);

            foreach (var effect in dispatch.Effects)
                await effect(realtime, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Realtime push failed for {EventType}; clients will catch up via REST.", message.EventType);
        }
    }

    private async Task BroadcastTicketChangeAsync(ConsumedIntegrationEvent message, CancellationToken ct)
    {
        if (!message.EventType.StartsWith("ticket.") && message.EventType != "sla.breached")
            return;

        // Every ticket.* and sla.breached event carries at least TenantId + TicketId; deserialize the
        // minimal envelope (extra JSON fields are ignored) to target the tenant's live-board group.
        if (JsonSerializer.Deserialize<TicketEnvelope>(message.PayloadJson, Json) is { } envelope)
            await realtime.TicketChangedAsync(envelope.TenantId, envelope.TicketId, message.EventType, ct);
    }

    private sealed record TicketEnvelope(Guid TenantId, Guid TicketId);
}
