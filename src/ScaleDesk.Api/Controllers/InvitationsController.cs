using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScaleDesk.Api.Common;
using ScaleDesk.Application.Contracts.Invitations;
using ScaleDesk.Application.Services;
using ScaleDesk.Domain.Constants;

namespace ScaleDesk.Api.Controllers;

[ApiController]
[Route("api/invitations")]
[Produces("application/json")]
public sealed class InvitationsController(IInvitationService invitations) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = Permissions.MembersInvite)]
    public async Task<IActionResult> Invite([FromBody] CreateInvitationRequest request, CancellationToken ct)
    {
        var result = await invitations.InviteAsync(request, ct);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : result.ToActionResult();
    }

    [HttpGet("pending")]
    [Authorize(Policy = Permissions.MembersInvite)]
    public async Task<IActionResult> ListPending(CancellationToken ct)
        => Ok(await invitations.ListPendingAsync(ct));

    [HttpPost("{id:guid}/revoke")]
    [Authorize(Policy = Permissions.MembersInvite)]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
        => (await invitations.RevokeAsync(id, ct)).ToActionResult();

    /// <summary>Anonymous accept: authorized by the out-of-band token, not a JWT.</summary>
    [HttpPost("accept")]
    [AllowAnonymous]
    public async Task<IActionResult> Accept([FromBody] AcceptInvitationRequest request, CancellationToken ct)
        => (await invitations.AcceptAsync(request, ct)).ToActionResult();
}
