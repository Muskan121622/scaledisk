using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScaleDesk.Api.Common;
using ScaleDesk.Application.Contracts.Automation;
using ScaleDesk.Application.Services;
using ScaleDesk.Domain.Constants;

namespace ScaleDesk.Api.Controllers;

/// <summary>Manage a tenant's workflow rules (§10). Reads need automation.read, writes automation.manage.</summary>
[ApiController]
[Route("api/automation-rules")]
[Authorize]
[Produces("application/json")]
public sealed class AutomationController(IAutomationService automation) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.AutomationRead)]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await automation.ListAsync(ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.AutomationRead)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => (await automation.GetAsync(id, ct)).ToActionResult();

    [HttpPost]
    [Authorize(Policy = Permissions.AutomationManage)]
    public async Task<IActionResult> Create([FromBody] CreateAutomationRuleRequest request, CancellationToken ct)
    {
        var result = await automation.CreateAsync(request, ct);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : result.ToActionResult();
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.AutomationManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAutomationRuleRequest request, CancellationToken ct)
        => (await automation.UpdateAsync(id, request, ct)).ToActionResult();

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.AutomationManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => (await automation.DeleteAsync(id, ct)).ToActionResult();
}
