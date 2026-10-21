namespace ScaleDesk.Application.Abstractions;

/// <summary>
/// Ambient tenant for the current request, resolved from the authenticated principal's
/// tenant claim. The persistence layer reads this to apply the tenant global query filter,
/// so a query can never silently cross tenants. Null tenant means "platform/no tenant scope".
/// </summary>
public interface ICurrentTenant
{
    Guid? TenantId { get; }

    bool IsAvailable { get; }

    /// <summary>Throws if no tenant is bound — use before any tenant-owned operation.</summary>
    Guid RequireTenant();
}
