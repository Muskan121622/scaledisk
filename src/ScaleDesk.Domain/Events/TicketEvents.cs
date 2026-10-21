using ScaleDesk.Domain.Common;
using ScaleDesk.Domain.Enums;

namespace ScaleDesk.Domain.Events;

// Integration-event candidates raised by the ticket aggregate. Kept lean (ids + coarse state)
// so downstream consumers (analytics, notifications, audit) can react without exposing PII.

public sealed record TicketCreated(
    Guid TenantId, Guid TicketId, Guid CustomerId, TicketPriority Priority, TicketCategory Category,
    DateTime? ResponseDueAt, DateTime? ResolutionDueAt) : IDomainEvent
{
    public string EventType => "ticket.created";
}

public sealed record TicketAssigned(
    Guid TenantId, Guid TicketId, Guid? AssigneeUserId) : IDomainEvent
{
    public string EventType => "ticket.assigned";
}

public sealed record TicketStatusChanged(
    Guid TenantId, Guid TicketId, TicketStatus Previous, TicketStatus Current) : IDomainEvent
{
    public string EventType => "ticket.status_changed";
}

public sealed record TicketResolved(
    Guid TenantId, Guid TicketId) : IDomainEvent
{
    public string EventType => "ticket.resolved";
}

public sealed record SlaBreached(
    Guid TenantId, Guid TicketId, string BreachType, DateTime DueAt) : IDomainEvent
{
    public string EventType => "sla.breached";
}
