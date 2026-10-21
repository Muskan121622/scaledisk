using ScaleDesk.Domain.Enums;

namespace ScaleDesk.Application.Contracts.Sla;

public sealed record CreateSlaPolicyRequest(
    string Name,
    TicketPriority Priority,
    int ResponseDueInMinutes,
    int ResolutionDueInMinutes,
    bool IsDefault);

public sealed record UpdateSlaPolicyRequest(
    string Name,
    TicketPriority Priority,
    int ResponseDueInMinutes,
    int ResolutionDueInMinutes,
    bool IsDefault);

public sealed record SlaPolicyResponse(
    Guid Id,
    string Name,
    TicketPriority Priority,
    int ResponseDueInMinutes,
    int ResolutionDueInMinutes,
    bool IsDefault);

/// <summary>
/// Minimal, serialization-friendly view of a tenant's SLA policies, cached per tenant (spec §20)
/// so ticket creation resolves deadlines from Redis instead of hitting Postgres on every write.
/// Kept separate from the entity so we never cache EF-tracked objects or their navigation graphs.
/// </summary>
public sealed record SlaPolicySnapshot(
    Guid Id,
    TicketPriority Priority,
    int ResponseDueInMinutes,
    int ResolutionDueInMinutes,
    bool IsDefault);
