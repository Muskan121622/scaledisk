using ScaleDesk.Domain.Automation;
using ScaleDesk.Domain.Common;

namespace ScaleDesk.Domain.Entities;

/// <summary>
/// A tenant-defined "when X happens, do Y" workflow rule (§10). Evaluated by the automation handler
/// off the integration-event stream, so rules react to real domain occurrences (a ticket being
/// created) without coupling publishers to this logic. Conditions and the ordered action list are
/// persisted as jsonb, keeping the schema stable while the rule vocabulary stays extensible.
/// </summary>
public class AutomationRule : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public string Name { get; set; } = default!;
    public string? Description { get; set; }

    /// <summary>Integration event type this rule reacts to, e.g. "ticket.created".</summary>
    public string TriggerEvent { get; set; } = default!;

    public bool IsEnabled { get; set; } = true;

    /// <summary>Lower runs first, so tenants can order overlapping rules deterministically.</summary>
    public int Priority { get; set; }

    public AutomationConditions Conditions { get; set; } = new();
    public List<AutomationAction> Actions { get; set; } = new();
}
