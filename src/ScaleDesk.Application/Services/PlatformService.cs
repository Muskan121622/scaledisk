using ScaleDesk.Application.Abstractions;
using ScaleDesk.Application.Abstractions.Repositories;
using ScaleDesk.Application.Common;
using ScaleDesk.Application.Contracts.Platform;
using ScaleDesk.Domain.Enums;

namespace ScaleDesk.Application.Services;

public sealed class PlatformService(IPlatformRepository platform, IUnitOfWork uow) : IPlatformService
{
    public Task<PlatformStatsResponse> GetStatsAsync(CancellationToken ct = default)
        => platform.GetStatsAsync(ct);

    public Task<PagedResult<TenantAdminResponse>> ListTenantsAsync(string? query, int page, int pageSize, CancellationToken ct = default)
        => platform.ListTenantsAsync(query, Math.Max(1, page), Math.Clamp(pageSize, 1, 100), ct);

    public async Task<Result<TenantAdminResponse>> GetTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        var tenant = await platform.GetTenantAsync(tenantId, ct);
        return tenant is null
            ? Result.Fail<TenantAdminResponse>("not_found", "No such workspace on the platform.")
            : Result.Ok(tenant);
    }

    public async Task<Result<TenantAdminResponse>> SetStatusAsync(Guid tenantId, TenantStatus status, CancellationToken ct = default)
    {
        var tenant = await platform.FindTenantAsync(tenantId, ct);
        if (tenant is null)
            return Result.Fail<TenantAdminResponse>("not_found", "No such workspace on the platform.");

        tenant.Status = status;
        platform.UpdateTenant(tenant);
        await uow.SaveChangesAsync(ct);

        var updated = await platform.GetTenantAsync(tenantId, ct);
        return Result.Ok(updated!);
    }
}
