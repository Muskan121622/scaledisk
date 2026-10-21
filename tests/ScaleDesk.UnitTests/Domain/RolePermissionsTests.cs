using ScaleDesk.Domain.Constants;
using ScaleDesk.Domain.Enums;

namespace ScaleDesk.UnitTests.Domain;

/// <summary>
/// Locks the RBAC contract. These assertions are the specification for "who can do what" and
/// will fail loudly if someone widens a role by accident.
/// </summary>
public class RolePermissionsTests
{
    [Fact]
    public void Viewer_can_read_but_not_create_tickets()
    {
        Assert.True(RolePermissions.Grants(UserRole.Viewer, Permissions.TicketsRead));
        Assert.False(RolePermissions.Grants(UserRole.Viewer, Permissions.TicketsCreate));
        Assert.False(RolePermissions.Grants(UserRole.Viewer, Permissions.BillingManage));
    }

    [Fact]
    public void Agent_can_work_tickets_but_not_manage_billing_or_members()
    {
        Assert.True(RolePermissions.Grants(UserRole.Agent, Permissions.TicketsCreate));
        Assert.True(RolePermissions.Grants(UserRole.Agent, Permissions.TicketsAssign));
        Assert.False(RolePermissions.Grants(UserRole.Agent, Permissions.BillingManage));
        Assert.False(RolePermissions.Grants(UserRole.Agent, Permissions.MembersRemove));
    }

    [Fact]
    public void Admin_cannot_be_escalated_to_owner_only_capabilities()
    {
        // Admin has everything except what only Owner implicitly holds (all permissions here).
        Assert.True(RolePermissions.Grants(UserRole.Admin, Permissions.BillingManage));
        Assert.True(RolePermissions.Grants(UserRole.Admin, Permissions.SettingsManage));
    }

    [Fact]
    public void Owner_has_every_defined_permission()
    {
        foreach (var permission in Permissions.All)
            Assert.True(RolePermissions.Grants(UserRole.Owner, permission), $"Owner missing {permission}");
    }

    [Fact]
    public void Every_permission_token_is_granted_to_someone()
    {
        var granted = Enum.GetValues<UserRole>().SelectMany(r => RolePermissions.For(r)).ToHashSet();
        foreach (var permission in Permissions.All)
            Assert.Contains(permission, granted);
    }
}
