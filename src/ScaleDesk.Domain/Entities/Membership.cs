using ScaleDesk.Domain.Common;
using ScaleDesk.Domain.Enums;

namespace ScaleDesk.Domain.Entities;

/// <summary>
/// Binds a global <see cref="User"/> to a <see cref="Tenant"/> with a role.
/// Tenant-owned: isolation is enforced so a membership row never leaks across tenants.
/// </summary>
public class Membership : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public Guid UserId { get; set; }
    public User? User { get; set; }

    public UserRole Role { get; set; }

    public MembershipStatus Status { get; set; } = MembershipStatus.Active;

    public Guid? InvitedByUserId { get; set; }
}
