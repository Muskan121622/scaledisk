using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScaleDesk.Api.Authorization;
using ScaleDesk.Api.Common;
using ScaleDesk.Application.Contracts.Platform;
using ScaleDesk.Application.Services;
using ScaleDesk.Domain.Enums;

namespace ScaleDesk.Api.Controllers;

/// <summary>
/// ScaleDesk HQ: the only cross-tenant surface in the API. Reachable exclusively with a
/// platform-scoped token (policy below + TenantScopeGuardMiddleware), never with a staff one.
/// </summary>
[ApiController]
[Route("api/platform")]
[Authorize(Policy = PlatformPolicies.Admin)]
[Produces("application/json")]
public sealed class PlatformController(IPlatformService platform) : ControllerBase
{
    [HttpGet("stats")]
    public async Task<IActionResult> Stats(CancellationToken ct)
        => Ok(await platform.GetStatsAsync(ct));

    [HttpGet("tenants")]
    public async Task<IActionResult> ListTenants(
        [FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await platform.ListTenantsAsync(q, page, pageSize, ct));

    [HttpGet("tenants/{id:guid}")]
    public async Task<IActionResult> GetTenant(Guid id, CancellationToken ct)
        => (await platform.GetTenantAsync(id, ct)).ToActionResult();

    [HttpPost("tenants/{id:guid}/suspend")]
    public async Task<IActionResult> Suspend(Guid id, CancellationToken ct)
        => (await platform.SetStatusAsync(id, TenantStatus.Suspended, ct)).ToActionResult();

    [HttpPost("tenants/{id:guid}/resume")]
    public async Task<IActionResult> Resume(Guid id, CancellationToken ct)
        => (await platform.SetStatusAsync(id, TenantStatus.Active, ct)).ToActionResult();
}
