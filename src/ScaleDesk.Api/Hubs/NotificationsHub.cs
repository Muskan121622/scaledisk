using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ScaleDesk.Application.Constants;

namespace ScaleDesk.Api.Hubs;

/// <summary>
/// Single inbound WebSocket endpoint for live updates (spec §21). Clients authenticate with the same
/// access token as REST (delivered via the "access_token" query string — see Program's JwtBearer
/// events). On connect each socket joins two server-side groups so pushes stay strictly scoped:
///   * user:{sub}   → the recipient's personal notifications (their bell only),
///   * tenant:{tid} → the workspace's shared live board.
/// Group membership is derived solely from validated JWT claims, never from client input, so a user
/// cannot subscribe to another tenant's feed.
/// </summary>
[Authorize]
public sealed class NotificationsHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var user = Context.User;
        if (user?.FindFirst(AppClaims.Subject)?.Value is { } userId)
            await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.User(userId));
        if (user?.FindFirst(AppClaims.TenantId)?.Value is { } tenantId)
            await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.Tenant(tenantId));

        await base.OnConnectedAsync();
    }
}

/// <summary>Canonical group-name scheme, shared by the hub and the notifier implementation.</summary>
internal static class HubGroups
{
    public static string User(string userId) => $"user:{userId}";
    public static string Tenant(string tenantId) => $"tenant:{tenantId}";
    public static string User(Guid userId) => $"user:{userId}";
    public static string Tenant(Guid tenantId) => $"tenant:{tenantId}";
}
