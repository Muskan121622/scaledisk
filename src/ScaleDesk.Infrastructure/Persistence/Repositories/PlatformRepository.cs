using Microsoft.EntityFrameworkCore;
using ScaleDesk.Application.Abstractions.Repositories;
using ScaleDesk.Application.Common;
using ScaleDesk.Application.Contracts.Platform;
using ScaleDesk.Domain.Entities;
using ScaleDesk.Domain.Enums;

namespace ScaleDesk.Infrastructure.Persistence.Repositories;

/// <summary>
/// Cross-tenant HQ queries. Every tenant-owned projection uses IgnoreQueryFilters explicitly:
/// platform requests carry no tenant so the ambient filter is already permissive, but the call
/// says what it means — these reads are deliberately global, never accidentally so.
/// </summary>
public sealed class PlatformRepository(ScaleDeskDbContext db) : IPlatformRepository
{
    private static readonly TicketStatus[] ClosedStatuses = [TicketStatus.Resolved, TicketStatus.Closed];

    public async Task<PagedResult<TenantAdminResponse>> ListTenantsAsync(string? query, int page, int pageSize, CancellationToken ct = default)
    {
        IQueryable<Tenant> rows = db.Tenants.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim().ToLower();
            rows = rows.Where(t => t.Name.ToLower().Contains(q) || t.Slug.ToLower().Contains(q));
        }

        var total = await rows.CountAsync(ct);
        var items = await rows
            .OrderBy(t => t.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(_project)
            .ToListAsync(ct);

        return new PagedResult<TenantAdminResponse>(items, total, page, pageSize);
    }

    public Task<TenantAdminResponse?> GetTenantAsync(Guid tenantId, CancellationToken ct = default) =>
        db.Tenants.AsNoTracking().Where(t => t.Id == tenantId).Select(_project).FirstOrDefaultAsync(ct);

    public async Task<PlatformStatsResponse> GetStatsAsync(CancellationToken ct = default)
    {
        var tenants = db.Tenants.AsNoTracking();
        var tickets = db.Tickets.IgnoreQueryFilters().AsNoTracking();

        return new PlatformStatsResponse(
            await tenants.CountAsync(ct),
            await tenants.CountAsync(t => t.Status == TenantStatus.Active, ct),
            await tenants.CountAsync(t => t.Status == TenantStatus.Suspended, ct),
            await db.Users.IgnoreQueryFilters().AsNoTracking().LongCountAsync(ct),
            await tickets.LongCountAsync(ct),
            await tickets.LongCountAsync(t => !ClosedStatuses.Contains(t.Status), ct));
    }

    public Task<Tenant?> FindTenantAsync(Guid tenantId, CancellationToken ct = default) =>
        db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct);

    public void UpdateTenant(Tenant tenant) => db.Tenants.Update(tenant);

    /// <summary>Single shared projection so list/detail rows are shaped identically.</summary>
    private readonly System.Linq.Expressions.Expression<Func<Tenant, TenantAdminResponse>> _project = t =>
        new TenantAdminResponse(
            t.Id, t.Name, t.Slug, t.Status, t.PlanTier, t.CreatedAt,
            db.Memberships.IgnoreQueryFilters().Count(m => m.TenantId == t.Id && m.Status == MembershipStatus.Active),
            db.Tickets.IgnoreQueryFilters().Count(x => x.TenantId == t.Id),
            db.Tickets.IgnoreQueryFilters().Count(x => x.TenantId == t.Id && !ClosedStatuses.Contains(x.Status)));
}
