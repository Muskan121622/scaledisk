using ScaleDesk.Domain.Common;

namespace ScaleDesk.Domain.Entities;

/// <summary>
/// An end customer of a tenant (not a platform login). Tenant-owned.
/// </summary>
public class Customer : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public string Name { get; set; } = default!;

    public string Email { get; set; } = default!;

    public string? Phone { get; set; }

    public string? Company { get; set; }

    /// <summary>Free-form tags, persisted as a Postgres text[] array.</summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>Extensible JSON blob (jsonb) for tenant-defined attributes.</summary>
    public string? Metadata { get; set; }

    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}
