using ScaleDesk.Domain.Common;

namespace ScaleDesk.Domain.Entities;

/// <summary>
/// A routing group within a tenant (e.g. Billing, Escalations). Tenant-owned.
/// </summary>
public class Team : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public string Name { get; set; } = default!;

    public string? Description { get; set; }

    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}
