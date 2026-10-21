namespace ScaleDesk.Application.Common.Caching;

/// <summary>
/// Single source of truth for cache keys, so a producer (TicketService) and an invalidator
/// (SlaService) can never drift on key format. Every key is tenant-prefixed to keep it correct
/// under multi-tenancy — a per-tenant namespace means one tenant's write can't bust another's.
/// </summary>
public static class CacheKeys
{
    public static string SlaPolicies(Guid tenantId) => $"sla:policies:{tenantId}";
}
