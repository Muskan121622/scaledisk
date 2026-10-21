using ScaleDesk.Domain.Common;

namespace ScaleDesk.Domain.Entities;

/// <summary>
/// Binds a global <see cref="User"/> login to one tenant's <see cref="Customer"/> record —
/// the customer-facing portal identity. Tenant-owned so the link itself is isolated per
/// workspace (a person can be a customer of two tenants with separate links, same login).
/// Deliberately NOT a Membership: customers stay outside the staff RBAC enum entirely.
/// </summary>
public class CustomerUser : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public Guid UserId { get; set; }

    public Guid CustomerId { get; set; }
}
