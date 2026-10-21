namespace ScaleDesk.Domain.Constants;

/// <summary>
/// Canonical, fine-grained permission tokens. Authorization policies are built from these
/// so the system never hard-codes a role check at a call site. Add new capabilities here.
/// </summary>
public static class Permissions
{
    public const string TicketsRead = "tickets.read";
    public const string TicketsCreate = "tickets.create";
    public const string TicketsUpdate = "tickets.update";
    public const string TicketsDelete = "tickets.delete";
    public const string TicketsAssign = "tickets.assign";
    public const string TicketsComment = "tickets.comment";

    public const string CustomersRead = "customers.read";
    public const string CustomersUpdate = "customers.update";

    public const string MembersInvite = "members.invite";
    public const string MembersRemove = "members.remove";

    public const string TeamsManage = "teams.manage";
    public const string SlaManage = "sla.manage";
    public const string AutomationRead = "automation.read";
    public const string AutomationManage = "automation.manage";
    public const string BillingManage = "billing.manage";
    public const string AuditRead = "audit.read";
    public const string SettingsManage = "settings.manage";

    public static readonly IReadOnlyCollection<string> All = new[]
    {
        TicketsRead, TicketsCreate, TicketsUpdate, TicketsDelete, TicketsAssign, TicketsComment,
        CustomersRead, CustomersUpdate,
        MembersInvite, MembersRemove,
        TeamsManage, SlaManage, AutomationRead, AutomationManage, BillingManage, AuditRead, SettingsManage
    };
}
