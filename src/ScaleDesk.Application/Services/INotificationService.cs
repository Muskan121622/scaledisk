using ScaleDesk.Application.Common;
using ScaleDesk.Application.Contracts.Notifications;

namespace ScaleDesk.Application.Services;

public interface INotificationService
{
    Task<IReadOnlyList<NotificationResponse>> ListAsync(bool unreadOnly, int take, CancellationToken ct = default);
    Task<Result<long>> UnreadCountAsync(CancellationToken ct = default);
    Task<Result<int>> MarkReadAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
    Task<Result<int>> MarkAllReadAsync(CancellationToken ct = default);
}
