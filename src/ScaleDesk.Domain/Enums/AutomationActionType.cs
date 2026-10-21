namespace ScaleDesk.Domain.Enums;

/// <summary>The mutation an automation action applies to a ticket when its rule matches (§10).</summary>
public enum AutomationActionType
{
    SetPriority = 0,
    SetStatus = 1,
    AssignTeam = 2,
    AssignUser = 3
}
