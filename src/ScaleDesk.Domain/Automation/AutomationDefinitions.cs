using ScaleDesk.Domain.Enums;

namespace ScaleDesk.Domain.Automation;

/// <summary>
/// Declarative match criteria for an automation rule (§10). Every populated field must hold for the
/// rule to fire (logical AND); a rule with no fields matches every event of its trigger type, giving
/// a safe catch-all. Kept deliberately small and structured (not a free-form expression language) so
/// rules stay auditable and serialisable to jsonb without a parser.
/// </summary>
public sealed record AutomationConditions
{
    public TicketPriority? Priority { get; init; }
    public TicketCategory? Category { get; init; }
    public TicketStatus? Status { get; init; }
    public string? SubjectContains { get; init; }

    /// <summary>Evaluate against a ticket snapshot. Null condition fields are treated as wildcards.</summary>
    public bool Matches(TicketPriority priority, TicketCategory category, TicketStatus status, string subject)
    {
        if (Priority is { } p && p != priority) return false;
        if (Category is { } c && c != category) return false;
        if (Status is { } s && s != status) return false;
        if (!string.IsNullOrWhiteSpace(SubjectContains)
            && subject!.ToLowerInvariant().Contains(SubjectContains.Trim().ToLowerInvariant()) == false)
            return false;
        return true;
    }
}

/// <summary>
/// A single mutation applied when a rule matches. The applicable field depends on <see cref="Type"/>
/// (SetPriority → Priority, SetStatus → Status, AssignTeam → TeamId, AssignUser → AssigneeUserId);
/// unrelated fields are ignored. Stored as an element of the rule's jsonb action list.
/// </summary>
public sealed record AutomationAction
{
    public AutomationActionType Type { get; init; }
    public TicketPriority? Priority { get; init; }
    public TicketStatus? Status { get; init; }
    public Guid? TeamId { get; init; }
    public Guid? AssigneeUserId { get; init; }
}
