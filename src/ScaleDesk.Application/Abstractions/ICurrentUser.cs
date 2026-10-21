using ScaleDesk.Domain.Enums;

namespace ScaleDesk.Application.Abstractions;

/// <summary>
/// The authenticated principal for the current request, decoded from the access token.
/// Carries the active tenant, role and the flattened permission set used for authorization.
/// </summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
    Guid? TenantId { get; }
    UserRole? Role { get; }
    IReadOnlySet<string> Permissions { get; }
    bool IsAuthenticated { get; }

    /// <summary>
    /// Customer record id for portal-scoped sessions (the cid claim). Null for staff and platform
    /// tokens — services treat its presence as "this request is a customer", switching from
    /// permission checks to ownership checks. Defaulted so existing implementers don't break.
    /// </summary>
    Guid? CustomerId => null;

    bool HasPermission(string permission);
}
