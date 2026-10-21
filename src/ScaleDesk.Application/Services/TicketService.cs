using System.Text.Json;
using ScaleDesk.Application.Abstractions;
using ScaleDesk.Application.Abstractions.Repositories;
using ScaleDesk.Application.Common;
using ScaleDesk.Application.Common.Caching;
using ScaleDesk.Application.Contracts.Sla;
using ScaleDesk.Application.Contracts.Tickets;
using ScaleDesk.Application.Exceptions;
using ScaleDesk.Domain.Constants;
using ScaleDesk.Domain.Entities;
using ScaleDesk.Domain.Enums;
using ScaleDesk.Domain.Events;

namespace ScaleDesk.Application.Services;

/// <summary>
/// Ticket lifecycle use-cases. Every operation is bound to the ambient tenant (via
/// <see cref="ICurrentTenant"/>) and gated on a fine-grained permission (via <see cref="ICurrentUser"/>),
/// so cross-tenant access and under-privileged actions are both rejected here, not at the edge.
/// Portal sessions (<see cref="ICurrentUser.CustomerId"/> set) share this exact pipeline — same SLA
/// stamping, outbox events and automation — but are gated on record OWNERSHIP instead of permissions.
/// </summary>
public sealed class TicketService : ITicketService
{
    private readonly ITicketRepository _tickets;
    private readonly ICustomerRepository _customers;
    private readonly ISlaPolicyRepository _slaPolicies;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly IEventCollector _events;
    private readonly IAppCache _cache;

    public TicketService(
        ITicketRepository tickets,
        ICustomerRepository customers,
        ISlaPolicyRepository slaPolicies,
        ICurrentTenant tenant,
        ICurrentUser user,
        IUnitOfWork uow,
        IClock clock,
        IEventCollector events,
        IAppCache cache)
    {
        _tickets = tickets;
        _customers = customers;
        _slaPolicies = slaPolicies;
        _tenant = tenant;
        _user = user;
        _uow = uow;
        _clock = clock;
        _events = events;
        _cache = cache;
    }

    public async Task<Result<TicketResponse>> CreateAsync(CreateTicketRequest request, CancellationToken ct = default)
    {
        // Portal customers skip RBAC entirely but may only file for THEMSELVES; staff pass an explicit customer.
        var portalCustomerId = _user.CustomerId;
        if (portalCustomerId is null)
            RequirePermission(Permissions.TicketsCreate);
        var tenantId = _tenant.RequireTenant();

        // Repository is tenant-filtered, so a foreign CustomerId resolves to null -> not a leak.
        var customer = await _customers.GetAsync(portalCustomerId ?? request.CustomerId, ct)
            ?? throw new NotFoundException("Customer not found in this workspace.");

        var now = _clock.UtcNow;
        var ticket = new Ticket
        {
            TenantId = tenantId,
            CustomerId = customer.Id,
            Subject = request.Subject.Trim(),
            Description = request.Description.Trim(),
            Status = TicketStatus.Open,
            Priority = request.Priority,
            Category = request.Category,
            // Portal intake cannot self-assign or self-route; automation rules do that off ticket.created.
            TeamId = portalCustomerId is null ? request.TeamId : null,
            AssigneeUserId = portalCustomerId is null ? request.AssigneeUserId : null,
            CreatedAt = now
        };

        // SLA engine: stamp the applicable policy's targets so the breach scanner has a baseline.
        await ApplySlaAsync(tenantId, ticket, now, ct);

        AppendEvent(ticket, "ticket.created",
            portalCustomerId is null ? $"Ticket created by {_user.UserId}" : "Ticket created via customer portal",
            new { request.Priority, request.Category });
        _events.Raise(new TicketCreated(tenantId, ticket.Id, customer.Id, ticket.Priority, ticket.Category,
            ticket.ResponseDueAt, ticket.ResolutionDueAt));
        await _tickets.AddAsync(ticket, ct);
        await _uow.SaveChangesAsync(ct);

        return Result.Ok(Map(ticket));
    }

    public async Task<Result<TicketResponse>> GetAsync(Guid id, CancellationToken ct = default)
    {
        if (_user.CustomerId is null)
            RequirePermission(Permissions.TicketsRead);
        var ticket = await GetOwnedAsync(id, ct);
        EnsurePortalOwnership(ticket);
        return Result.Ok(Map(ticket));
    }

    public async Task<PagedResult<TicketResponse>> ListAsync(TicketQuery query, CancellationToken ct = default)
    {
        var portalCustomerId = _user.CustomerId;
        if (portalCustomerId is null)
            RequirePermission(Permissions.TicketsRead);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        // Portal: the same tenant-filtered query, additionally pinned to the caller's own customer id.
        var items = await _tickets.ListAsync(query.Status, query.Priority, query.AssigneeUserId, page, pageSize, ct, portalCustomerId);
        var total = await _tickets.CountAsync(query.Status, query.Priority, query.AssigneeUserId, ct, portalCustomerId);

        return new PagedResult<TicketResponse>(items.Select(Map).ToList(), total, page, pageSize);
    }

    public async Task<Result<TicketResponse>> UpdateStatusAsync(Guid id, UpdateTicketStatusRequest request, CancellationToken ct = default)
    {
        RequirePermission(Permissions.TicketsUpdate);
        var ticket = await GetOwnedAsync(id, ct);

        var previous = ticket.Status;
        ticket.Status = request.Status;
        ticket.UpdatedAt = _clock.UtcNow;

        if (request.Status == TicketStatus.Resolved && ticket.ResolvedAt is null)
            ticket.ResolvedAt = _clock.UtcNow;
        else if (request.Status is TicketStatus.Open or TicketStatus.InProgress)
            ticket.ResolvedAt = null;

        ApplySlaOnStatusChange(ticket, _clock.UtcNow);

        AppendEvent(ticket, "ticket.status_changed", $"Status {previous} → {request.Status}", new { previous, current = request.Status });
        _events.Raise(new TicketStatusChanged(ticket.TenantId, ticket.Id, previous, request.Status));
        if (request.Status == TicketStatus.Resolved)
            _events.Raise(new TicketResolved(ticket.TenantId, ticket.Id));
        await _uow.SaveChangesAsync(ct);

        return Result.Ok(Map(ticket));
    }

    public async Task<Result<TicketResponse>> AssignAsync(Guid id, AssignTicketRequest request, CancellationToken ct = default)
    {
        RequirePermission(Permissions.TicketsAssign);
        var ticket = await GetOwnedAsync(id, ct);

        ticket.AssigneeUserId = request.AssigneeUserId;
        ticket.TeamId = request.TeamId ?? ticket.TeamId;
        if (request.AssigneeUserId is not null && ticket.Status == TicketStatus.Open)
            ticket.Status = TicketStatus.InProgress;
        ticket.UpdatedAt = _clock.UtcNow;

        AppendEvent(ticket, "ticket.assigned", request.AssigneeUserId is null ? "Unassigned" : $"Assigned to {request.AssigneeUserId}",
            new { request.AssigneeUserId, request.TeamId });
        _events.Raise(new TicketAssigned(ticket.TenantId, ticket.Id, request.AssigneeUserId));
        await _uow.SaveChangesAsync(ct);

        return Result.Ok(Map(ticket));
    }

    public async Task<Result<TicketCommentResponse>> AddCommentAsync(Guid id, AddTicketCommentRequest request, CancellationToken ct = default)
    {
        var portalCustomerId = _user.CustomerId;
        if (portalCustomerId is null)
            RequirePermission(Permissions.TicketsComment);
        var ticket = await GetOwnedAsync(id, ct);
        EnsurePortalOwnership(ticket);
        var now = _clock.UtcNow;

        var comment = new TicketComment
        {
            TenantId = ticket.TenantId,
            TicketId = ticket.Id,
            AuthorUserId = portalCustomerId is null ? _user.UserId : null,
            CustomerId = portalCustomerId,
            Body = request.Body.Trim(),
            // Internal notes are a staff-only concept; customer replies are always public.
            IsInternal = portalCustomerId is null && request.IsInternal,
            CreatedAt = now
        };

        // First-response SLA stamping only counts STAFF replies — a customer bumping their own
        // ticket must not mark the agent's response as done.
        if (portalCustomerId is null && !request.IsInternal && ticket.FirstResponseAt is null)
        {
            ticket.FirstResponseAt = now;

            // A public reply is the first response: late but present still counts as a breach (§9).
            if (ticket.ResponseDueAt is not null && now > ticket.ResponseDueAt && !ticket.SlaBreached)
            {
                ticket.SlaBreached = true;
                _events.Raise(new SlaBreached(ticket.TenantId, ticket.Id, "first_response", ticket.ResponseDueAt.Value));
            }
        }

        _tickets.AddComment(comment);
        AppendEvent(ticket, "ticket.commented", "New reply", new { comment.IsInternal });
        await _uow.SaveChangesAsync(ct);

        return Result.Ok(new TicketCommentResponse(
            comment.Id, comment.AuthorUserId, comment.CustomerId, comment.Body, comment.IsInternal, comment.CreatedAt));
    }

    public async Task<IReadOnlyList<TicketCommentResponse>> GetCommentsAsync(Guid id, CancellationToken ct = default)
    {
        var portalCustomerId = _user.CustomerId;
        if (portalCustomerId is null)
            RequirePermission(Permissions.TicketsRead);
        // GetOwnedAsync enforces the tenant filter — a foreign id throws NotFound before we ever hit comments.
        var ticket = await GetOwnedAsync(id, ct);
        EnsurePortalOwnership(ticket);
        var rows = await _tickets.GetCommentsAsync(id, ct);
        if (portalCustomerId is not null)
            rows = rows.Where(c => !c.IsInternal).ToList(); // internal notes stay staff-side
        return rows
            .Select(c => new TicketCommentResponse(c.Id, c.AuthorUserId, c.CustomerId, c.Body, c.IsInternal, c.CreatedAt))
            .ToList();
    }

    public async Task<Result<TicketResponse>> CloseFromCustomerAsync(Guid id, CancellationToken ct = default)
    {
        if (_user.CustomerId is null)
            throw new ForbiddenException("Only customer portal sessions may confirm closure; staff use the status endpoint.");
        var ticket = await GetOwnedAsync(id, ct);
        EnsurePortalOwnership(ticket);

        if (ticket.Status != TicketStatus.Resolved)
            return Result.Fail<TicketResponse>("not_resolved", "Only resolved tickets can be closed.");

        var previous = ticket.Status;
        ticket.Status = TicketStatus.Closed;
        ticket.UpdatedAt = _clock.UtcNow;

        AppendEvent(ticket, "ticket.status_changed", $"Status {previous} → Closed (confirmed by customer)",
            new { previous, current = TicketStatus.Closed });
        _events.Raise(new TicketStatusChanged(ticket.TenantId, ticket.Id, previous, TicketStatus.Closed));
        await _uow.SaveChangesAsync(ct);

        return Result.Ok(Map(ticket));
    }

    public async Task<IReadOnlyList<TicketEventResponse>> GetEventsAsync(Guid id, CancellationToken ct = default)
    {
        RequirePermission(Permissions.TicketsRead);
        // Ownership check first (same as comments), then read the append-only timeline oldest-first.
        await GetOwnedAsync(id, ct);
        var rows = await _tickets.GetEventsAsync(id, ct);
        return rows
            .Select(e => new TicketEventResponse(e.Id, e.Type, e.Summary, e.ActorUserId, e.CreatedAt))
            .ToList();
    }

    /// <summary>
    /// Resolves the tenant's SLA policy for the ticket priority — explicit match first, then the
    /// workspace default — and stamps the resulting deadlines. The tenant's policy set is read
    /// cache-aside (spec §20): ticket writes are far more frequent than SLA edits, so this keeps a
    /// hot path off Postgres. No matching policy leaves the ticket unscored.
    /// </summary>
    private async Task ApplySlaAsync(Guid tenantId, Ticket ticket, DateTime now, CancellationToken ct)
    {
        var policies = await GetPoliciesCachedAsync(tenantId, ct);
        var policy =
            policies.FirstOrDefault(p => p.Priority == ticket.Priority && !p.IsDefault)
            ?? policies.FirstOrDefault(p => p.Priority == ticket.Priority)
            ?? policies.FirstOrDefault(p => p.IsDefault);
        if (policy is null)
            return;

        ticket.SlaPolicyId = policy.Id;
        ticket.ResponseDueAt = now.AddMinutes(policy.ResponseDueInMinutes);
        ticket.ResolutionDueAt = now.AddMinutes(policy.ResolutionDueInMinutes);
    }

    private async Task<List<SlaPolicySnapshot>> GetPoliciesCachedAsync(Guid tenantId, CancellationToken ct)
    {
        var key = CacheKeys.SlaPolicies(tenantId);
        var cached = await _cache.GetAsync<List<SlaPolicySnapshot>>(key, ct);
        if (cached is not null)
            return cached;

        var fromDb = await _slaPolicies.ListAsync(ct);
        var snapshot = fromDb
            .Select(p => new SlaPolicySnapshot(p.Id, p.Priority, p.ResponseDueInMinutes, p.ResolutionDueInMinutes, p.IsDefault))
            .ToList();
        await _cache.SetAsync(key, snapshot, TimeSpan.FromMinutes(5), ct);
        return snapshot;
    }

    /// <summary>
    /// Immediate breach check for the resolution clock (the scanner covers tickets that lapse while
    /// nobody is writing to them). Resolution on time clears the breach flag for reopened tickets.
    /// </summary>
    private void ApplySlaOnStatusChange(Ticket ticket, DateTime now)
    {
        if (ticket.Status is TicketStatus.Resolved or TicketStatus.Closed)
        {
            if (ticket.ResolutionDueAt is not null && now > ticket.ResolutionDueAt && !ticket.SlaBreached)
            {
                ticket.SlaBreached = true;
                _events.Raise(new SlaBreached(ticket.TenantId, ticket.Id, "resolution", ticket.ResolutionDueAt.Value));
            }
            else if (ticket.ResolutionDueAt is null || now <= ticket.ResolutionDueAt)
            {
                ticket.SlaBreached = false;
            }
        }
    }

    private async Task<Ticket> GetOwnedAsync(Guid id, CancellationToken ct)
    {
        // GetAsync is tenant-filtered at the persistence layer; a foreign id returns null.
        var ticket = await _tickets.GetAsync(id, ct)
            ?? throw new NotFoundException("Ticket not found in this workspace.");
        return ticket;
    }

    /// <summary>
    /// Portal sessions may only touch tickets that belong to THEIR customer record. Same shape as
    /// the tenant leak answer: NotFound, not Forbidden — a portal user learns nothing about rows
    /// they do not own.
    /// </summary>
    private void EnsurePortalOwnership(Ticket ticket)
    {
        if (_user.CustomerId is { } cid && ticket.CustomerId != cid)
            throw new NotFoundException("Ticket not found in this workspace.");
    }

    private void RequirePermission(string permission)
    {
        if (!_user.HasPermission(permission))
            throw new ForbiddenException($"Missing required permission '{permission}'.");
    }

    private void AppendEvent(Ticket ticket, string type, string summary, object payload)
    {
        _tickets.AddEvent(new TicketEvent
        {
            TenantId = ticket.TenantId,
            TicketId = ticket.Id,
            Type = type,
            ActorUserId = _user.UserId,
            Summary = summary,
            Payload = JsonSerializer.Serialize(payload),
            CreatedAt = _clock.UtcNow
        });
    }

    private static TicketResponse Map(Ticket t) => new(
        t.Id, t.TenantId, t.CustomerId, t.Subject, t.Description, t.Status, t.Priority, t.Category,
        t.AssigneeUserId, t.TeamId, t.CreatedAt, t.ResolvedAt, t.RowVersion,
        t.SlaPolicyId, t.ResponseDueAt, t.ResolutionDueAt, t.SlaBreached);
}
