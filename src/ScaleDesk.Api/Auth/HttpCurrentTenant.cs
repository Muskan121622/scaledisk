using System.Security.Claims;
using ScaleDesk.Application.Abstractions;
using ScaleDesk.Application.Constants;
using ScaleDesk.Application.Exceptions;

namespace ScaleDesk.Api.Auth;

/// <summary>
/// Binds the ambient tenant for the current request from the authenticated token's "tid" claim.
/// Registered per-request; the persistence layer reads it to apply the tenant query filter, so
/// every query is automatically scoped without a repository having to remember a WHERE clause.
/// </summary>
public sealed class HttpCurrentTenant(IHttpContextAccessor accessor) : ICurrentTenant
{
    public Guid? TenantId =>
        Guid.TryParse(accessor.HttpContext?.User.FindFirstValue(AppClaims.TenantId), out var id) ? id : null;

    public bool IsAvailable => TenantId.HasValue;

    public Guid RequireTenant() =>
        TenantId ?? throw new TenantNotResolvedException();
}
