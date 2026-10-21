using ScaleDesk.Domain.Common;
using ScaleDesk.Domain.Enums;

namespace ScaleDesk.Domain.Entities;

/// <summary>
/// A customer organization (tenant) on the platform. Platform-level: NOT tenant-owned.
/// </summary>
public class Tenant : BaseEntity
{
    public string Name { get; set; } = default!;

    /// <summary>URL-safe, globally unique handle for the organization.</summary>
    public string Slug { get; set; } = default!;

    public TenantStatus Status { get; set; } = TenantStatus.Active;

    public string? PlanTier { get; set; }

    public ICollection<Membership> Memberships { get; set; } = new List<Membership>();
}
