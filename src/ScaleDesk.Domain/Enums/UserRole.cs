namespace ScaleDesk.Domain.Enums;

/// <summary>
/// Coarse-grained membership role. Authorization is NOT decided by role alone —
/// roles are merely a convenient bundle of fine-grained <see cref="Constants.Permissions"/>.
/// </summary>
public enum UserRole
{
    Owner = 0,
    Admin = 1,
    Manager = 2,
    Agent = 3,
    Viewer = 4
}
