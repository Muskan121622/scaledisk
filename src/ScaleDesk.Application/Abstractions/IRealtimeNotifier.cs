using ScaleDesk.Application.Contracts.Notifications;

namespace ScaleDesk.Application.Abstractions;

/// <summary>
/// Push seam for real-time delivery to connected clients (spec §21). The Application/Infrastructure
/// layers stay free of any SignalR dependency — they only know this contract; the Api supplies the
/// concrete hub-backed implementation at composition time. Fire-and-forget from the caller's point
/// of view: a failed push must never unwind an already-committed projection.
/// </summary>
public interface IRealtimeNotifier
{
    /// <summary>Deliver a freshly-created notification to its recipient's personal channel.</summary>
    Task NotificationCreatedAsync(Guid recipientUserId, NotificationResponse notification, CancellationToken ct = default);

    /// <summary>Broadcast a ticket mutation to the tenant's live board channel.</summary>
    Task TicketChangedAsync(Guid tenantId, Guid ticketId, string eventType, CancellationToken ct = default);
}
