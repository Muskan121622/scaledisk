using ScaleDesk.Domain.Enums;

namespace ScaleDesk.Application.Contracts.Automation;

/// <summary>Match criteria a rule evaluates against an incoming ticket. Null fields are wildcards.</summary>
public sealed record AutomationConditionsDto(
    TicketPriority? Priority,
    TicketCategory? Category,
    TicketStatus? Status,
    string? SubjectContains);

/// <summary>One mutation to apply when a rule matches. Which field matters depends on <see cref="Type"/>.</summary>
public sealed record AutomationActionDto(
    AutomationActionType Type,
    TicketPriority? Priority,
    TicketStatus? Status,
    Guid? TeamId,
    Guid? AssigneeUserId);

public sealed record CreateAutomationRuleRequest(
    string Name,
    string? Description,
    string TriggerEvent,
    int? Priority,
    bool? IsEnabled,
    AutomationConditionsDto? Conditions,
    IReadOnlyList<AutomationActionDto>? Actions);

public sealed record UpdateAutomationRuleRequest(
    string Name,
    string? Description,
    string TriggerEvent,
    int? Priority,
    bool IsEnabled,
    AutomationConditionsDto? Conditions,
    IReadOnlyList<AutomationActionDto>? Actions);

public sealed record AutomationRuleResponse(
    Guid Id,
    string Name,
    string? Description,
    string TriggerEvent,
    bool IsEnabled,
    int Priority,
    AutomationConditionsDto Conditions,
    IReadOnlyList<AutomationActionDto> Actions);
