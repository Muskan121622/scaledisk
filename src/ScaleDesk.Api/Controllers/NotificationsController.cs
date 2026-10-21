using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScaleDesk.Api.Common;
using ScaleDesk.Application.Contracts.Notifications;
using ScaleDesk.Application.Services;

namespace ScaleDesk.Api.Controllers;

/// <summary>
/// The signed-in user's personal notification feed (spec §22). Intentionally guarded only by
/// <c>[Authorize]</c> — no permission policy — because the service scopes every query to the current
/// user id and the tenant query filter scopes it to their workspace. There is nothing to grant: a
/// user can only ever read and dismiss their own notifications.
/// </summary>
[ApiController]
[Route("api/notifications")]
[Authorize]
[Produces("application/json")]
public sealed class NotificationsController(INotificationService notifications) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] bool unreadOnly = false,
        [FromQuery] int take = 30,
        CancellationToken ct = default)
        => Ok(await notifications.ListAsync(unreadOnly, take, ct));

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount(CancellationToken ct)
        => (await notifications.UnreadCountAsync(ct)).ToActionResult();

    [HttpPost("mark-read")]
    public async Task<IActionResult> MarkRead([FromBody] MarkNotificationsReadRequest request, CancellationToken ct)
        => (await notifications.MarkReadAsync(request.Ids, ct)).ToActionResult();

    [HttpPost("mark-all-read")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
        => (await notifications.MarkAllReadAsync(ct)).ToActionResult();
}
