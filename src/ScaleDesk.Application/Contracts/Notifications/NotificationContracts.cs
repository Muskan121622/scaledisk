namespace ScaleDesk.Application.Contracts.Notifications;

public sealed record NotificationResponse(
    Guid Id,
    string Type,
    string Title,
    string? Body,
    Guid? RelatedTicketId,
    bool IsRead,
    DateTime CreatedAt);

/// <summary>Ids of notifications the caller wants marked read (own feed only).</summary>
public sealed record MarkNotificationsReadRequest(IReadOnlyCollection<Guid> Ids);
