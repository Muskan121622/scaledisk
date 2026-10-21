using ScaleDesk.Domain.Enums;

namespace ScaleDesk.Domain.Constants;

/// <summary>
/// Pure domain policy mapping a membership <see cref="UserRole"/> to its permission set.
/// This is the single source of truth for what each role can do; the API builds authorization
/// policies from the resulting tokens rather than checking roles inline.
/// </summary>
public static class RolePermissions
{
    private static readonly Dictionary<UserRole, HashSet<string>> Map = new()
    {
        [UserRole.Owner] = new(Permissions.All),

        [UserRole.Admin] =
        [
            Permissions.TicketsRead, Permissions.TicketsCreate, Permissions.TicketsUpdate,
            Permissions.TicketsDelete, Permissions.TicketsAssign, Permissions.TicketsComment,
            Permissions.CustomersRead, Permissions.CustomersUpdate,
            Permissions.MembersInvite, Permissions.MembersRemove,
            Permissions.TeamsManage, Permissions.SlaManage, Permissions.BillingManage,
            Permissions.AutomationRead, Permissions.AutomationManage,
            Permissions.AuditRead, Permissions.SettingsManage
        ],

        [UserRole.Manager] =
        [
            Permissions.TicketsRead, Permissions.TicketsCreate, Permissions.TicketsUpdate,
            Permissions.TicketsAssign, Permissions.TicketsComment,
            Permissions.CustomersRead, Permissions.CustomersUpdate,
            Permissions.MembersInvite, Permissions.TeamsManage, Permissions.SlaManage,
            Permissions.AutomationRead, Permissions.AutomationManage,
            Permissions.AuditRead
        ],

        [UserRole.Agent] =
        [
            Permissions.TicketsRead, Permissions.TicketsCreate, Permissions.TicketsUpdate,
            Permissions.TicketsAssign, Permissions.TicketsComment,
            Permissions.CustomersRead
        ],

        [UserRole.Viewer] =
        [
            Permissions.TicketsRead, Permissions.CustomersRead, Permissions.AuditRead
        ]
    };

    public static IReadOnlySet<string> For(UserRole role) =>
        Map.TryGetValue(role, out var set) ? set : new HashSet<string>();

    public static bool Grants(UserRole role, string permission) =>
        Map.TryGetValue(role, out var set) && set.Contains(permission);
}
