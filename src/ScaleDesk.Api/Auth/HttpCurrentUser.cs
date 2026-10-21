using System.Security.Claims;
using ScaleDesk.Application.Abstractions;
using ScaleDesk.Application.Constants;
using ScaleDesk.Domain.Enums;

namespace ScaleDesk.Api.Auth;

/// <summary>
/// Decodes the authenticated principal from the access token: user id, active tenant, role and
/// the flattened permission set. Used both by the authorization handler and by services for
/// object-level checks (defense in depth).
/// </summary>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public Guid? UserId =>
        Guid.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? Principal?.FindFirstValue("sub"), out var id) ? id : null;

    public Guid? TenantId =>
        Guid.TryParse(Principal?.FindFirstValue(AppClaims.TenantId), out var id) ? id : null;

    public UserRole? Role =>
        Enum.TryParse<UserRole>(Principal?.FindFirstValue(AppClaims.Role), out var role) ? role : null;

    /// <summary>Present only on portal-scoped tokens; drives customer ownership checks in services.</summary>
    public Guid? CustomerId =>
        Guid.TryParse(Principal?.FindFirstValue(AppClaims.CustomerId), out var id) ? id : null;

    public IReadOnlySet<string> Permissions => LoadPermissions();

    private IReadOnlySet<string> LoadPermissions()
    {
        var values = Principal?.FindAll(AppClaims.Permission).Select(c => c.Value) ?? [];
        return new HashSet<string>(values);
    }

    public bool IsAuthenticated =>
        Principal?.Identity?.IsAuthenticated == true && UserId.HasValue;

    public bool HasPermission(string permission) => Permissions.Contains(permission);
}
