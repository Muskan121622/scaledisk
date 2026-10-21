using ScaleDesk.Application.Common;
using ScaleDesk.Application.Contracts.Platform;
using ScaleDesk.Domain.Enums;

namespace ScaleDesk.Application.Services;

/// <summary>
/// ScaleDesk HQ view: cross-tenant directory, platform counters and suspend/resume lifecycle.
/// Authorization is enforced by the platform policy at the controller plus the request scope
/// guard; there are no per-tenant permission checks here because the caller IS the platform.
/// </summary>
public interface IPlatformService
{
    Task<PlatformStatsResponse> GetStatsAsync(CancellationToken ct = default);
    Task<PagedResult<TenantAdminResponse>> ListTenantsAsync(string? query, int page, int pageSize, CancellationToken ct = default);
    Task<Result<TenantAdminResponse>> GetTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<Result<TenantAdminResponse>> SetStatusAsync(Guid tenantId, TenantStatus status, CancellationToken ct = default);
}
