using ScaleDesk.Application.Common;
using ScaleDesk.Application.Contracts.Platform;
using ScaleDesk.Domain.Entities;
using ScaleDesk.Domain.Enums;

namespace ScaleDesk.Application.Abstractions.Repositories;

/// <summary>
/// Cross-tenant reads for ScaleDesk HQ. Only ever resolved on a request with NO tenant bound
/// (platform-scoped token), where the EF tenant filter and RLS are permissive by design — the
/// platform authorization policy plus the scope guard are what keep this unreachable otherwise.
/// </summary>
public interface IPlatformRepository
{
    Task<PagedResult<TenantAdminResponse>> ListTenantsAsync(string? query, int page, int pageSize, CancellationToken ct = default);
    Task<TenantAdminResponse?> GetTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<PlatformStatsResponse> GetStatsAsync(CancellationToken ct = default);
    Task<Tenant?> FindTenantAsync(Guid tenantId, CancellationToken ct = default);
    void UpdateTenant(Tenant tenant);
}
