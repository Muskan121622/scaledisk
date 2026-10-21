namespace ScaleDesk.Domain.Common;

/// <summary>
/// Marker for entities whose rows belong to exactly one tenant.
/// Every <see cref="ITenantOwned"/> entity is subject to the tenant global query filter
/// and cross-tenant isolation tests. Platform-level entities (Tenant, User) do NOT implement this.
/// </summary>
public interface ITenantOwned
{
    Guid TenantId { get; set; }
}
