using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using ScaleDesk.Application.Constants;

namespace ScaleDesk.Api.Middleware;

/// <summary>
/// Single choke point tying token <c>scope</c> to URL space. The EF tenant filter is permissive
/// when no tenant is bound (workers, seeding, login), so a token must never be able to wander:
///   * staff    -> anywhere under /api EXCEPT /api/platform and /api/portal, and must carry a tenant;
///   * platform -> /api/platform only (its missing tenant claim would otherwise open every tenant);
///   * portal   -> /api/portal only (customer identity lives in the cid claim).
/// Runs after authentication; anonymous endpoints ([AllowAnonymous]) never reach a decision here
/// because there is no principal. Defense in depth: authorization policies still apply per route.
/// </summary>
public sealed class TenantScopeGuardMiddleware(RequestDelegate next)
{
    public async Task Invoke(HttpContext context)
    {
        var user = context.User;
        var path = context.Request.Path;

        if (user.Identity?.IsAuthenticated == true && path.StartsWithSegments("/api"))
        {
            var scope = user.FindFirstValue(AppClaims.Scope) ?? AppClaims.ScopeStaff;
            var allowed = scope switch
            {
                AppClaims.ScopePlatform => path.StartsWithSegments("/api/platform"),
                AppClaims.ScopePortal => path.StartsWithSegments("/api/portal"),
                // Staff (incl. legacy tokens minted before scopes existed): tenant claim is mandatory.
                _ => !path.StartsWithSegments("/api/platform")
                     && !path.StartsWithSegments("/api/portal")
                     && !string.IsNullOrEmpty(user.FindFirstValue(AppClaims.TenantId))
            };

            if (!allowed)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "This session type cannot access the requested area.",
                    Type = "https://scaledesk.dev/errors/token_scope"
                });
                return;
            }
        }

        await next(context);
    }
}
