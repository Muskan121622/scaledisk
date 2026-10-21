namespace ScaleDesk.Application.Contracts.Teams;

public sealed record CreateTeamRequest(string Name, string? Description);

public sealed record UpdateTeamRequest(string Name, string? Description);

public sealed record TeamResponse(
    Guid Id,
    Guid TenantId,
    string Name,
    string? Description,
    DateTime CreatedAt);
