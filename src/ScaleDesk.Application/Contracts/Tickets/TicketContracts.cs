using ScaleDesk.Domain.Enums;

namespace ScaleDesk.Application.Contracts.Tickets;

public sealed record CreateTicketRequest(
    Guid CustomerId,
    string Subject,
    string Description,
    TicketPriority Priority,
    TicketCategory Category,
    Guid? TeamId,
    Guid? AssigneeUserId);

public sealed record UpdateTicketStatusRequest(TicketStatus Status);

public sealed record AssignTicketRequest(Guid? AssigneeUserId, Guid? TeamId);

public sealed record AddTicketCommentRequest(string Body, bool IsInternal);

public sealed record TicketResponse(
    Guid Id,
    Guid TenantId,
    Guid CustomerId,
    string Subject,
    string Description,
    TicketStatus Status,
    TicketPriority Priority,
    TicketCategory Category,
    Guid? AssigneeUserId,
    Guid? TeamId,
    DateTime CreatedAt,
    DateTime? ResolvedAt,
    uint RowVersion,
    Guid? SlaPolicyId,
    DateTime? ResponseDueAt,
    DateTime? ResolutionDueAt,
    bool SlaBreached);

public sealed record TicketCommentResponse(
    Guid Id,
    Guid? AuthorUserId,
    Guid? CustomerId,
    string Body,
    bool IsInternal,
    DateTime CreatedAt);

public sealed record TicketEventResponse(
    Guid Id,
    string Type,
    string Summary,
    Guid? ActorUserId,
    DateTime CreatedAt);

public sealed record TicketQuery(
    TicketStatus? Status,
    TicketPriority? Priority,
    Guid? AssigneeUserId,
    int Page = 1,
    int PageSize = 20);
