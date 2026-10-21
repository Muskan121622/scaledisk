using ScaleDesk.Domain.Enums;

namespace ScaleDesk.Application.Contracts.Invitations;

public sealed record CreateInvitationRequest(string Email, UserRole Role);

/// <summary>
/// Anonymous accept payload. The token arrives out-of-band (email link), never from a JWT, and the
/// invitee sets their own password here — possession of the mailbox is what verifies the email.
/// </summary>
public sealed record AcceptInvitationRequest(
    string Token,
    string FirstName,
    string LastName,
    string Password);

public sealed record InvitationResponse(
    Guid Id,
    string Email,
    UserRole Role,
    InvitationStatus Status,
    DateTime ExpiresAt,
    DateTime CreatedAt);

public sealed record AcceptInvitationResponse(
    string Email,
    string TenantName,
    UserRole Role);
