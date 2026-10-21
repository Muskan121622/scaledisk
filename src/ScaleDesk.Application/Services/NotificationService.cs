using ScaleDesk.Application.Abstractions;
using ScaleDesk.Application.Abstractions.Repositories;
using ScaleDesk.Application.Common;
using ScaleDesk.Application.Contracts.Notifications;
using ScaleDesk.Application.Exceptions;

namespace ScaleDesk.Application.Services;

/// <summary>
/// Personal notification feed for the signed-in user (spec §22). Notifications are addressed to a
/// recipient and scoped by the tenant query filter, so a user only ever sees their own, in their
/// current workspace — no extra permission token is required because it is not another user's data.
/// </summary>
public sealed class NotificationService : INotificationService
{
    private readonly INotificationRepository _notifications;
    private readonly ICurrentUser _user;

    public NotificationService(INotificationRepository notifications, ICurrentUser user)
    {
        _notifications = notifications;
        _user = user;
    }

    public async Task<IReadOnlyList<NotificationResponse>> ListAsync(bool unreadOnly, int take, CancellationToken ct = default)
    {
        var userId = RequireUserId();
        take = Math.Clamp(take, 1, 100);
        var items = await _notifications.ListForUserAsync(userId, unreadOnly, take, ct);
        return items.Select(Map).ToList();
    }

    public async Task<Result<long>> UnreadCountAsync(CancellationToken ct = default)
    {
        var userId = RequireUserId();
        return Result.Ok(await _notifications.CountUnreadAsync(userId, ct));
    }

    public async Task<Result<int>> MarkReadAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        var userId = RequireUserId();
        if (ids.Count == 0)
            return Result.Ok(0);
        var changed = await _notifications.MarkReadAsync(userId, ids, ct);
        return Result.Ok(changed);
    }

    public async Task<Result<int>> MarkAllReadAsync(CancellationToken ct = default)
    {
        var userId = RequireUserId();
        return Result.Ok(await _notifications.MarkAllReadAsync(userId, ct));
    }

    private Guid RequireUserId() =>
        _user.UserId ?? throw new ForbiddenException("No authenticated user for this request.");

    private static NotificationResponse Map(Domain.Entities.Notification n) => new(
        n.Id, n.Type, n.Title, n.Body, n.RelatedTicketId, n.IsRead, n.CreatedAt);
}
