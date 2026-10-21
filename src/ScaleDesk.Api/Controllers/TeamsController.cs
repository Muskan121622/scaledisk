using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScaleDesk.Api.Common;
using ScaleDesk.Application.Contracts.Teams;
using ScaleDesk.Application.Services;
using ScaleDesk.Domain.Constants;

namespace ScaleDesk.Api.Controllers;

[ApiController]
[Route("api/teams")]
[Authorize]
[Produces("application/json")]
public sealed class TeamsController(ITeamService teams) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.TicketsRead)]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await teams.ListAsync(ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.TicketsRead)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => (await teams.GetAsync(id, ct)).ToActionResult();

    [HttpPost]
    [Authorize(Policy = Permissions.TeamsManage)]
    public async Task<IActionResult> Create([FromBody] CreateTeamRequest request, CancellationToken ct)
    {
        var result = await teams.CreateAsync(request, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { id = result.Value.Id }, result.Value)
            : result.ToActionResult();
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.TeamsManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTeamRequest request, CancellationToken ct)
        => (await teams.UpdateAsync(id, request, ct)).ToActionResult();

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.TeamsManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => (await teams.DeleteAsync(id, ct)).ToActionResult();
}
