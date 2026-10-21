using Microsoft.AspNetCore.SignalR;
using ScaleDesk.Application.Abstractions;
using ScaleDesk.Application.Contracts.Notifications;

namespace ScaleDesk.Api.Hubs;

/// <summary>
/// Api-side adapter binding the framework-free <see cref="IRealtimeNotifier"/> seam to a concrete
/// SignalR hub via <see cref="IHubContext{T}"/>. Living in the composition root keeps SignalR out of
/// Application/Infrastructure: the event processor only knows the interface and never references the
/// hub, yet real-time fan-out is wired end to end at runtime. Registered as a singleton because
/// IHubContext is itself a thread-safe singleton.
/// </summary>
public sealed class SignalRRealtimeNotifier(IHubContext<NotificationsHub> hub) : IRealtimeNotifier
{
    public Task NotificationCreatedAsync(Guid recipientUserId, NotificationResponse notification, CancellationToken ct = default)
        => hub.Clients.Group(HubGroups.User(recipientUserId)).SendAsync("notification", notification, ct);

    public Task TicketChangedAsync(Guid tenantId, Guid ticketId, string eventType, CancellationToken ct = default)
        => hub.Clients.Group(HubGroups.Tenant(tenantId)).SendAsync(
            "ticketChanged", new { ticketId, eventType, changedAt = DateTime.UtcNow }, ct);
}
