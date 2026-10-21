using ScaleDesk.Domain.Enums;

namespace ScaleDesk.Application.Contracts.Portal;

/// <summary>
/// Customer self-registration. Claims the tenant's existing <see cref="ScaleDesk.Domain.Entities.Customer"/>
/// record with this email (link-by-email) or creates a new one, then binds/creates the global login.
/// </summary>
public sealed record PortalRegisterRequest(
    string OrganizationSlug,
    string Email,
    string Password,
    string FirstName,
    string LastName);

public sealed record PortalLoginRequest(string OrganizationSlug, string Email, string Password);

public sealed record PortalRefreshRequest(string RefreshToken);

/// <summary>Portal ticket intake — deliberately no customer/team/assignee fields: identity comes
/// from the cid claim and routing stays with the tenant's automation rules.</summary>
public sealed record CreatePortalTicketRequest(
    string Subject,
    string Description,
    TicketPriority Priority,
    TicketCategory Category);

public sealed record AddPortalCommentRequest(string Body);
