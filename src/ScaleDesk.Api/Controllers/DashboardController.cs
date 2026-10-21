using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScaleDesk.Application.Abstractions;
using ScaleDesk.Application.Contracts.Dashboard;
using ScaleDesk.Domain.Constants;

namespace ScaleDesk.Api.Controllers;

/// <summary>
/// Stage 5F.1 read-model for the Overview screen. One small endpoint that returns everything the
/// dashboard needs so the client does not fan out to five list calls just to render four cards.
/// </summary>
[ApiController]
[Route("api/dashboard")]
[Authorize(Policy = Permissions.TicketsRead)]
[Produces("application/json")]
public sealed class DashboardController(IDashboardQuery dashboard) : ControllerBase
{
    [HttpGet("stats")]
    public async Task<DashboardStatsResponse> Stats([FromQuery] int lookbackDays = 14, CancellationToken ct = default)
        => await dashboard.QueryAsync(lookbackDays, ct);
}
