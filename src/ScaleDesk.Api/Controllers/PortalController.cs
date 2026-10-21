using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ScaleDesk.Api.Common;
using ScaleDesk.Application.Contracts.Portal;
using ScaleDesk.Application.Contracts.Tickets;
using ScaleDesk.Application.Services;
using ScaleDesk.Domain.Enums;

namespace ScaleDesk.Api.Controllers;

/// <summary>
/// Customer-facing surface (J3). Anonymous entry points sit behind the shared "auth" rate-limit
/// policy; the rest requires a scope=portal token — TenantScopeGuardMiddleware rejects staff and
/// platform tokens here, and portal tokens are rejected everywhere else. Ticket endpoints reuse
/// ITicketService with no permission policies: the cid claim + row ownership IS the authorization
/// model there (see TicketService portal branches).
/// </summary>
[ApiController]
[Route("api/portal")]
[Produces("application/json")]
public sealed class PortalController(IPortalService portal, ITicketService tickets) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register([FromBody] PortalRegisterRequest request, CancellationToken ct)
    {
        var result = await portal.RegisterAsync(request, ClientIp, ct);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : result.ToActionResult();
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login([FromBody] PortalLoginRequest request, CancellationToken ct)
        => (await portal.LoginAsync(request, ClientIp, ct)).ToActionResult();

    [AllowAnonymous]
    [HttpPost("refresh")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Refresh([FromBody] PortalRefreshRequest request, CancellationToken ct)
        => (await portal.RefreshAsync(request, ClientIp, ct)).ToActionResult();

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromQuery] string refreshToken, CancellationToken ct)
        => (await portal.LogoutAsync(refreshToken, ct)).ToActionResult();

    // --- Tickets -----------------------------------------------------------------

    [Authorize]
    [HttpGet("tickets")]
    public async Task<IActionResult> ListTickets(
        [FromQuery] TicketStatus? status,
        [FromQuery] TicketPriority? priority,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        // No assignee filter: the portal branch in TicketService pins the query to the caller's cid.
        var query = new TicketQuery(status, priority, null, page, pageSize);
        return Ok(await tickets.ListAsync(query, ct));
    }

    [Authorize]
    [HttpGet("tickets/{id:guid}")]
    public async Task<IActionResult> GetTicket(Guid id, CancellationToken ct)
        => (await tickets.GetAsync(id, ct)).ToActionResult();

    [Authorize]
    [HttpPost("tickets")]
    public async Task<IActionResult> CreateTicket([FromBody] CreatePortalTicketRequest request, CancellationToken ct)
    {
        // CustomerId placeholder — the portal branch replaces it from the cid claim, and
        // TeamId/Assignee stay null: routing is the tenant's automation rules' job.
        var result = await tickets.CreateAsync(
            new CreateTicketRequest(Guid.Empty, request.Subject, request.Description, request.Priority, request.Category, null, null), ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetTicket), new { id = result.Value.Id }, result.Value)
            : result.ToActionResult();
    }

    [Authorize]
    [HttpGet("tickets/{id:guid}/comments")]
    public async Task<IActionResult> ListComments(Guid id, CancellationToken ct)
        => Ok(await tickets.GetCommentsAsync(id, ct));

    [Authorize]
    [HttpPost("tickets/{id:guid}/comments")]
    public async Task<IActionResult> AddComment(Guid id, [FromBody] AddPortalCommentRequest request, CancellationToken ct)
    {
        // IsInternal is structurally false for customers — the service also overrides it defensively.
        var result = await tickets.AddCommentAsync(id, new AddTicketCommentRequest(request.Body, false), ct);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : result.ToActionResult();
    }

    /// <summary>Customer confirms closure of their own Resolved ticket (the "case closed?" handshake).</summary>
    [Authorize]
    [HttpPost("tickets/{id:guid}/close")]
    public async Task<IActionResult> CloseTicket(Guid id, CancellationToken ct)
        => (await tickets.CloseFromCustomerAsync(id, ct)).ToActionResult();

    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();
}
