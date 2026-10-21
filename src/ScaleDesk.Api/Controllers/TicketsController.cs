using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScaleDesk.Api.Common;
using ScaleDesk.Application.Contracts.Tickets;
using ScaleDesk.Application.Services;
using ScaleDesk.Domain.Constants;
using ScaleDesk.Domain.Enums;

namespace ScaleDesk.Api.Controllers;

[ApiController]
[Route("api/tickets")]
[Authorize]
[Produces("application/json")]
public sealed class TicketsController(ITicketService tickets) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.TicketsRead)]
    public async Task<IActionResult> List(
        [FromQuery] TicketStatus? status,
        [FromQuery] TicketPriority? priority,
        [FromQuery] Guid? assignee,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new TicketQuery(status, priority, assignee, page, pageSize);
        return Ok(await tickets.ListAsync(query, ct));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.TicketsRead)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => (await tickets.GetAsync(id, ct)).ToActionResult();

    [HttpPost]
    [Authorize(Policy = Permissions.TicketsCreate)]
    public async Task<IActionResult> Create([FromBody] CreateTicketRequest request, CancellationToken ct)
    {
        var result = await tickets.CreateAsync(request, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { id = result.Value.Id }, result.Value)
            : result.ToActionResult();
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Policy = Permissions.TicketsUpdate)]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateTicketStatusRequest request, CancellationToken ct)
        => (await tickets.UpdateStatusAsync(id, request, ct)).ToActionResult();

    [HttpPatch("{id:guid}/assign")]
    [Authorize(Policy = Permissions.TicketsAssign)]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignTicketRequest request, CancellationToken ct)
        => (await tickets.AssignAsync(id, request, ct)).ToActionResult();

    [HttpPost("{id:guid}/comments")]
    [Authorize(Policy = Permissions.TicketsComment)]
    public async Task<IActionResult> Comment(Guid id, [FromBody] AddTicketCommentRequest request, CancellationToken ct)
    {
        var result = await tickets.AddCommentAsync(id, request, ct);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : result.ToActionResult();
    }

    [HttpGet("{id:guid}/comments")]
    [Authorize(Policy = Permissions.TicketsRead)]
    public async Task<IActionResult> ListComments(Guid id, CancellationToken ct)
        => Ok(await tickets.GetCommentsAsync(id, ct));

    [HttpGet("{id:guid}/events")]
    [Authorize(Policy = Permissions.TicketsRead)]
    public async Task<IActionResult> ListEvents(Guid id, CancellationToken ct)
        => Ok(await tickets.GetEventsAsync(id, ct));
}
