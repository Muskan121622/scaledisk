using ScaleDesk.Domain.Common;
using ScaleDesk.Domain.Enums;

namespace ScaleDesk.Domain.Entities;

/// <summary>
/// An outstanding invitation to join a tenant workspace. The accept token itself is never stored —
/// only its SHA-256 hash (same discipline as refresh tokens), so a database leak cannot be used to
/// claim memberships. One pending invitation per (tenant, email); re-inviting rotates the token.
/// </summary>
public class Invitation : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public string Email { get; set; } = default!;

    public UserRole Role { get; set; }

    /// <summary>SHA-256 hash of the opaque accept token; sent in the invitation email link.</summary>
    public string TokenHash { get; set; } = default!;

    public InvitationStatus Status { get; set; } = InvitationStatus.Pending;

    public Guid? InvitedByUserId { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? AcceptedAt { get; set; }

    public bool IsUsable(DateTime utcNow) =>
        Status == InvitationStatus.Pending && ExpiresAt > utcNow;
}
