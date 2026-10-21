using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScaleDesk.Api.Common;
using ScaleDesk.Application.Contracts.Sla;
using ScaleDesk.Application.Services;
using ScaleDesk.Domain.Constants;

namespace ScaleDesk.Api.Controllers;

[ApiController]
[Route("api/sla-policies")]
[Authorize(Policy = Permissions.SlaManage)]
[Produces("application/json")]
public sealed class SlaPoliciesController(ISlaService sla) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await sla.ListAsync(ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSlaPolicyRequest request, CancellationToken ct)
    {
        var result = await sla.CreateAsync(request, ct);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : result.ToActionResult();
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSlaPolicyRequest request, CancellationToken ct)
        => (await sla.UpdateAsync(id, request, ct)).ToActionResult();

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => (await sla.DeleteAsync(id, ct)).ToActionResult();
}
