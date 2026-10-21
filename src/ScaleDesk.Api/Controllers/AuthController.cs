using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ScaleDesk.Api.Common;
using ScaleDesk.Application.Abstractions;
using ScaleDesk.Application.Contracts.Auth;
using ScaleDesk.Application.Contracts.Platform;
using ScaleDesk.Application.Services;
using ScaleDesk.Domain.Constants;

namespace ScaleDesk.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController(IAuthService auth, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Creates a new organization (tenant) and its owner account.</summary>
    [AllowAnonymous]
    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var result = await auth.RegisterAsync(request, ClientIp, ct);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : result.ToActionResult();
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
        => (await auth.LoginAsync(request, ClientIp, ct)).ToActionResult();

    /// <summary>
    /// Public "Try the demo" path. Delegates to the ordinary credential login with a fixed
    /// seeded account — the caller never sees the password, and the response is the standard
    /// <see cref="AuthTokensResponse"/>. Rate-limited via the same "auth" policy as /login.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("demo")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Demo(CancellationToken ct)
    {
        var request = new LoginRequest(DemoWorkspace.OwnerEmail, DemoWorkspace.OwnerPassword, DemoWorkspace.Slug);
        return (await auth.LoginAsync(request, ClientIp, ct)).ToActionResult();
    }

    /// <summary>
    /// ScaleDesk HQ login. Mints an access-only, platform-scoped token (no tenant, no refresh
    /// lineage) that is valid solely under /api/platform — see TenantScopeGuardMiddleware.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("platform/login")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> PlatformLogin([FromBody] PlatformLoginRequest request, CancellationToken ct)
        => (await auth.PlatformLoginAsync(request, ct)).ToActionResult();

    [AllowAnonymous]
    [HttpPost("refresh")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken ct)
        => (await auth.RefreshAsync(request, ClientIp, ct)).ToActionResult();

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromQuery] string refreshToken, CancellationToken ct)
        => (await auth.LogoutAsync(refreshToken, ct)).ToActionResult();

    /// <summary>Returns the decoded identity for the active tenant (drives the client's RBAC UI).</summary>
    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var userId = currentUser.UserId!.Value;
        var tenantId = currentUser.TenantId!.Value;
        return (await auth.GetCurrentUserAsync(userId, tenantId, ct)).ToActionResult();
    }

    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();
}
