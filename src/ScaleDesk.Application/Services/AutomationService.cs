using ScaleDesk.Application.Abstractions;
using ScaleDesk.Application.Abstractions.Repositories;
using ScaleDesk.Application.Common;
using ScaleDesk.Application.Contracts.Automation;
using ScaleDesk.Application.Exceptions;
using ScaleDesk.Domain.Automation;
using ScaleDesk.Domain.Constants;
using ScaleDesk.Domain.Entities;

namespace ScaleDesk.Application.Services;

/// <summary>
/// CRUD for a tenant's automation rules (spec §10). Reads gate on <c>automation.read</c>, writes on
/// <c>automation.manage</c>. This service only defines rules; the engine that reacts to events lives
/// on the consumer side (Infrastructure) so rule evaluation is decoupled from the request path.
/// </summary>
public sealed class AutomationService : IAutomationService
{
    // Triggers the engine currently knows how to react to. Guarding here gives immediate feedback
    // instead of silently accepting a rule that would never fire.
    private static readonly HashSet<string> SupportedTriggers = new(StringComparer.Ordinal) { "ticket.created" };

    private readonly IAutomationRuleRepository _rules;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;

    public AutomationService(
        IAutomationRuleRepository rules,
        ICurrentTenant tenant,
        ICurrentUser user,
        IUnitOfWork uow,
        IClock clock)
    {
        _rules = rules;
        _tenant = tenant;
        _user = user;
        _uow = uow;
        _clock = clock;
    }

    public async Task<IReadOnlyList<AutomationRuleResponse>> ListAsync(CancellationToken ct = default)
    {
        RequirePermission(Permissions.AutomationRead);
        var rules = await _rules.ListAsync(ct);
        return rules.Select(Map).ToList();
    }

    public async Task<Result<AutomationRuleResponse>> GetAsync(Guid id, CancellationToken ct = default)
    {
        RequirePermission(Permissions.AutomationRead);
        return Result.Ok(Map(await GetOwnedAsync(id, ct)));
    }

    public async Task<Result<AutomationRuleResponse>> CreateAsync(CreateAutomationRuleRequest request, CancellationToken ct = default)
    {
        RequirePermission(Permissions.AutomationManage);
        var tenantId = _tenant.RequireTenant();
        Validate(request.Name, request.TriggerEvent, request.Actions);

        var rule = new AutomationRule
        {
            TenantId = tenantId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            TriggerEvent = request.TriggerEvent.Trim(),
            IsEnabled = request.IsEnabled ?? true,
            Priority = request.Priority ?? 0,
            Conditions = ToDomain(request.Conditions),
            Actions = ToDomain(request.Actions),
            CreatedAt = _clock.UtcNow
        };

        await _rules.AddAsync(rule, ct);
        await _uow.SaveChangesAsync(ct);
        return Result.Ok(Map(rule));
    }

    public async Task<Result<AutomationRuleResponse>> UpdateAsync(Guid id, UpdateAutomationRuleRequest request, CancellationToken ct = default)
    {
        RequirePermission(Permissions.AutomationManage);
        var rule = await GetOwnedAsync(id, ct);
        Validate(request.Name, request.TriggerEvent, request.Actions);

        rule.Name = request.Name.Trim();
        rule.Description = request.Description?.Trim();
        rule.TriggerEvent = request.TriggerEvent.Trim();
        rule.IsEnabled = request.IsEnabled;
        rule.Priority = request.Priority ?? rule.Priority;
        rule.Conditions = ToDomain(request.Conditions);
        rule.Actions = ToDomain(request.Actions);
        rule.UpdatedAt = _clock.UtcNow;

        _rules.Update(rule);
        await _uow.SaveChangesAsync(ct);
        return Result.Ok(Map(rule));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        RequirePermission(Permissions.AutomationManage);
        var rule = await GetOwnedAsync(id, ct);

        _rules.Remove(rule);
        await _uow.SaveChangesAsync(ct);
        return Result.Ok();
    }

    private async Task<AutomationRule> GetOwnedAsync(Guid id, CancellationToken ct) =>
        await _rules.GetAsync(id, ct)
            ?? throw new NotFoundException("Automation rule not found in this workspace.");

    private void RequirePermission(string permission)
    {
        if (!_user.HasPermission(permission))
            throw new ForbiddenException($"Missing required permission '{permission}'.");
    }

    private static void Validate(string name, string triggerEvent, IReadOnlyList<AutomationActionDto>? actions)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(name))
            errors["name"] = ["Rule name is required."];
        if (string.IsNullOrWhiteSpace(triggerEvent))
            errors["triggerEvent"] = ["A trigger event is required."];
        else if (!SupportedTriggers.Contains(triggerEvent.Trim()))
            errors["triggerEvent"] = [$"Unsupported trigger '{triggerEvent}'. Supported: {string.Join(", ", SupportedTriggers)}."];

        if (actions is null || actions.Count == 0)
            errors["actions"] = ["At least one action is required."];
        else
        {
            foreach (var (index, error) in actions.Select((a, i) => ValidateAction(a, i)).Where(x => x.error is not null))
                errors[$"actions[{index}]"] = [error!];
        }

        if (errors.Count > 0)
            throw new ValidationException(errors);
    }

    private static (int index, string? error) ValidateAction(AutomationActionDto action, int index) => action.Type switch
    {
        Domain.Enums.AutomationActionType.SetPriority when action.Priority is null => (index, "SetPriority requires a priority."),
        Domain.Enums.AutomationActionType.SetStatus when action.Status is null => (index, "SetStatus requires a status."),
        Domain.Enums.AutomationActionType.AssignTeam when action.TeamId is null => (index, "AssignTeam requires a teamId."),
        Domain.Enums.AutomationActionType.AssignUser when action.AssigneeUserId is null => (index, "AssignUser requires an assigneeUserId."),
        _ => (index, null)
    };

    private static AutomationConditions ToDomain(AutomationConditionsDto? dto) => new()
    {
        Priority = dto?.Priority,
        Category = dto?.Category,
        Status = dto?.Status,
        SubjectContains = dto?.SubjectContains
    };

    private static List<AutomationAction> ToDomain(IReadOnlyList<AutomationActionDto>? dtos) =>
        (dtos ?? Array.Empty<AutomationActionDto>()).Select(a => new AutomationAction
        {
            Type = a.Type,
            Priority = a.Priority,
            Status = a.Status,
            TeamId = a.TeamId,
            AssigneeUserId = a.AssigneeUserId
        }).ToList();

    private static AutomationConditionsDto ToDto(AutomationConditions c) =>
        new(c.Priority, c.Category, c.Status, c.SubjectContains);

    private static AutomationActionDto ToDto(AutomationAction a) =>
        new(a.Type, a.Priority, a.Status, a.TeamId, a.AssigneeUserId);

    private static AutomationRuleResponse Map(AutomationRule r) => new(
        r.Id, r.Name, r.Description, r.TriggerEvent, r.IsEnabled, r.Priority,
        ToDto(r.Conditions), r.Actions.Select(ToDto).ToList());
}
