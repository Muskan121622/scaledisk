using Microsoft.AspNetCore.Authorization;
using ScaleDesk.Application.Constants;

namespace ScaleDesk.Api.Authorization;

/// <summary>
/// Policy names that are not permission tokens (those are generated from Permissions.All).
/// </summary>
public static class PlatformPolicies
{
    public const string Admin = "platform_admin";
}

/// <summary>
/// Satisfied only by tokens whose scope claim marks a ScaleDesk HQ session. Deliberately an
/// assertion on the raw claim, not a permission: platform admins hold no tenant permissions —
/// their power is cross-tenant visibility, which deserves its own explicit gate.
/// </summary>
public sealed class PlatformAdminRequirement : IAuthorizationRequirement;

public sealed class PlatformAdminAuthorizationHandler : AuthorizationHandler<PlatformAdminRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PlatformAdminRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && context.User.FindFirst(AppClaims.Scope)?.Value == AppClaims.ScopePlatform)
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
