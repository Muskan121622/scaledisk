using ScaleDesk.Application.Abstractions;
using ScaleDesk.Application.Abstractions.Repositories;
using ScaleDesk.Application.Common;
using ScaleDesk.Application.Contracts.Teams;
using ScaleDesk.Application.Exceptions;
using ScaleDesk.Domain.Constants;
using ScaleDesk.Domain.Entities;
using ScaleDesk.Domain.Events;

namespace ScaleDesk.Application.Services;

/// <summary>
/// Routing teams within a tenant. Listing is open to anyone who can read tickets (they need team
/// ids to route work); mutating teams requires the <c>teams.manage</c> capability.
/// </summary>
public sealed class TeamService : ITeamService
{
    private readonly ITeamRepository _teams;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly IEventCollector _events;

    public TeamService(
        ITeamRepository teams,
        ICurrentTenant tenant,
        ICurrentUser user,
        IUnitOfWork uow,
        IClock clock,
        IEventCollector events)
    {
        _teams = teams;
        _tenant = tenant;
        _user = user;
        _uow = uow;
        _clock = clock;
        _events = events;
    }

    public async Task<Result<TeamResponse>> CreateAsync(CreateTeamRequest request, CancellationToken ct = default)
    {
        RequirePermission(Permissions.TeamsManage);
        var tenantId = _tenant.RequireTenant();
        var name = NormalizeName(request.Name);

        if (await _teams.FindByNameAsync(name, ct) is not null)
            throw new ConflictException($"A team named '{name}' already exists in this workspace.");

        var team = new Team
        {
            TenantId = tenantId,
            Name = name,
            Description = TrimOrNull(request.Description),
            CreatedAt = _clock.UtcNow
        };

        await _teams.AddAsync(team, ct);
        _events.Raise(new TeamCreated(tenantId, team.Id, team.Name));
        await _uow.SaveChangesAsync(ct);

        return Result.Ok(Map(team));
    }

    public async Task<Result<TeamResponse>> GetAsync(Guid id, CancellationToken ct = default)
    {
        RequirePermission(Permissions.TicketsRead);
        var team = await GetOwnedAsync(id, ct);
        return Result.Ok(Map(team));
    }

    public async Task<IReadOnlyList<TeamResponse>> ListAsync(CancellationToken ct = default)
    {
        RequirePermission(Permissions.TicketsRead);
        var teams = await _teams.ListAsync(ct);
        return teams.Select(Map).ToList();
    }

    public async Task<Result<TeamResponse>> UpdateAsync(Guid id, UpdateTeamRequest request, CancellationToken ct = default)
    {
        RequirePermission(Permissions.TeamsManage);
        var team = await GetOwnedAsync(id, ct);
        var name = NormalizeName(request.Name);

        var existing = await _teams.FindByNameAsync(name, ct);
        if (existing is not null && existing.Id != team.Id)
            throw new ConflictException($"Another team already uses the name '{name}'.");

        team.Name = name;
        team.Description = TrimOrNull(request.Description);
        team.UpdatedAt = _clock.UtcNow;

        _teams.Update(team);
        await _uow.SaveChangesAsync(ct);

        return Result.Ok(Map(team));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        RequirePermission(Permissions.TeamsManage);
        var team = await GetOwnedAsync(id, ct);

        // TeamId on tickets is SetNull on delete, so removing a team never destroys ticket history.
        _teams.Remove(team);
        await _uow.SaveChangesAsync(ct);

        return Result.Ok();
    }

    private async Task<Team> GetOwnedAsync(Guid id, CancellationToken ct) =>
        await _teams.GetAsync(id, ct)
            ?? throw new NotFoundException("Team not found in this workspace.");

    private void RequirePermission(string permission)
    {
        if (!_user.HasPermission(permission))
            throw new ForbiddenException($"Missing required permission '{permission}'.");
    }

    private static string NormalizeName(string name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            throw new ValidationException("name", "Team name is required.");
        if (trimmed.Length > 120)
            throw new ValidationException("name", "Team name must be 120 characters or fewer.");
        return trimmed;
    }

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static TeamResponse Map(Team t) => new(
        t.Id, t.TenantId, t.Name, t.Description, t.CreatedAt);
}
