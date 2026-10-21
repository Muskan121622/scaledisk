using Microsoft.AspNetCore.Authorization;
using ScaleDesk.Application.Abstractions;

namespace ScaleDesk.Api.Authorization;

/// <summary>
/// A requirement satisfied when the current principal holds a specific permission token.
/// Policies are generated from <see cref="ScaleDesk.Domain.Constants.Permissions.All"/> at startup,
/// so controllers declare intent ("tickets.create") and never check roles inline.
/// </summary>
public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;

public sealed class PermissionAuthorizationHandler(ICurrentUser currentUser)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated == true && currentUser.HasPermission(requirement.Permission))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
