using Microsoft.Extensions.Logging;
using ScaleDesk.Application.Abstractions;
using ScaleDesk.Application.Abstractions.Repositories;
using ScaleDesk.Application.Common;
using ScaleDesk.Application.Common.Security;
using ScaleDesk.Application.Constants;
using ScaleDesk.Application.Contracts.Auth;
using ScaleDesk.Application.Contracts.Platform;
using ScaleDesk.Domain.Constants;
using ScaleDesk.Domain.Entities;
using ScaleDesk.Domain.Enums;
using ScaleDesk.Application.Options;

namespace ScaleDesk.Application.Services;

/// <summary>
/// Orchestrates the authentication lifecycle: organization registration, credential login with
/// progressive lockout, and refresh-token rotation. It depends only on abstractions, so it is
/// unit-testable without EF or HTTP.
/// </summary>
public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly ITenantRepository _tenants;
    private readonly IMembershipRepository _memberships;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAccessTokenIssuer _tokenIssuer;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly AuthOptions _options;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository users,
        ITenantRepository tenants,
        IMembershipRepository memberships,
        IRefreshTokenRepository refreshTokens,
        IPasswordHasher passwordHasher,
        IAccessTokenIssuer tokenIssuer,
        IUnitOfWork uow,
        IClock clock,
        AuthOptions options,
        ILogger<AuthService> logger)
    {
        _users = users;
        _tenants = tenants;
        _memberships = memberships;
        _refreshTokens = refreshTokens;
        _passwordHasher = passwordHasher;
        _tokenIssuer = tokenIssuer;
        _uow = uow;
        _clock = clock;
        _options = options;
        _logger = logger;
    }

    public async Task<Result<AuthTokensResponse>> RegisterAsync(
        RegisterRequest request, string? clientIp, CancellationToken ct = default)
    {
        var email = Normalize(request.Email);
        var slug = Normalize(request.OrganizationSlug);

        if (await _users.GetByEmailAsync(email, ct) is not null)
            return Result.Fail<AuthTokensResponse>("email_taken", "That email is already registered.");

        if (await _tenants.GetBySlugAsync(slug, ct) is not null)
            return Result.Fail<AuthTokensResponse>("slug_taken", "That workspace URL is already taken.");

        var now = _clock.UtcNow;

        var tenant = new Tenant
        {
            Name = request.OrganizationName.Trim(),
            Slug = slug,
            Status = TenantStatus.Active,
            PlanTier = "free",
            CreatedAt = now
        };

        var user = new User
        {
            Email = email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            EmailConfirmed = false, // email verification flow lands in a later stage
            CreatedAt = now
        };

        var membership = new Membership
        {
            TenantId = tenant.Id,
            UserId = user.Id,
            Role = UserRole.Owner,
            Status = MembershipStatus.Active,
            CreatedAt = now
        };

        await _tenants.AddAsync(tenant, ct);
        await _users.AddAsync(user, ct);
        await _memberships.AddAsync(membership, ct);

        var tokens = await IssueSessionAsync(user, tenant.Id, UserRole.Owner, clientIp, ct);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Registered tenant {TenantSlug} with owner {UserId}", tenant.Slug, user.Id);
        return Result.Ok(tokens);
    }

    public async Task<Result<AuthTokensResponse>> LoginAsync(
        LoginRequest request, string? clientIp, CancellationToken ct = default)
    {
        var email = Normalize(request.Email);
        var slug = Normalize(request.OrganizationSlug);

        var user = await _users.GetByEmailAsync(email, ct);
        var tenant = await _tenants.GetBySlugAsync(slug, ct);

        // Same generic failure for unknown user / tenant / bad password: no account enumeration.
        if (user is null || tenant is null)
            return Result.Fail<AuthTokensResponse>("invalid_credentials", "Invalid email or password.");

        // Platform lifecycle: a suspended workspace cannot authenticate staff (existing sessions
        // ride out the short access-token life; no new sessions are minted).
        if (tenant.Status != TenantStatus.Active)
            return Result.Fail<AuthTokensResponse>("tenant_suspended", "This workspace is suspended. Contact ScaleDesk support.");

        var membership = await _memberships.GetAsync(user.Id, tenant.Id, ct);
        if (membership is null || membership.Status != MembershipStatus.Active)
            return Result.Fail<AuthTokensResponse>("invalid_credentials", "Invalid email or password.");

        var now = _clock.UtcNow;
        if (user.IsLockedOut(now))
            return Result.Fail<AuthTokensResponse>("locked_out", "Account temporarily locked. Try again later.");

        if (!_passwordHasher.Verify(user.PasswordHash, request.Password))
        {
            user.AccessFailedCount++;
            if (user.AccessFailedCount >= _options.MaxFailedAccessAttempts)
            {
                user.LockoutUntil = now.AddMinutes(_options.LockoutMinutes);
                user.AccessFailedCount = 0;
            }
            await _uow.SaveChangesAsync(ct);
            return Result.Fail<AuthTokensResponse>("invalid_credentials", "Invalid email or password.");
        }

        // Success: clear lockout state and transparently upgrade an outdated hash.
        user.AccessFailedCount = 0;
        user.LockoutUntil = null;
        user.LastLoginAt = now;
        if (_passwordHasher.ShouldRehash(user.PasswordHash))
            user.PasswordHash = _passwordHasher.Hash(request.Password);

        var tokens = await IssueSessionAsync(user, tenant.Id, membership.Role, clientIp, ct);
        await _uow.SaveChangesAsync(ct);
        return Result.Ok(tokens);
    }

    /// <summary>
    /// ScaleDesk HQ sign-in. Deliberately access-only (no refresh lineage): the token carries no
    /// tenant or role claims, so the request scope guard rejects it on every tenant endpoint and
    /// only the platform policy accepts it. Same lockout discipline as staff logins.
    /// </summary>
    public async Task<Result<PlatformSessionResponse>> PlatformLoginAsync(
        PlatformLoginRequest request, CancellationToken ct = default)
    {
        var email = Normalize(request.Email);
        var user = await _users.GetByEmailAsync(email, ct);

        // Non-platform accounts get the generic failure: no enumeration of who is HQ staff.
        if (user is null || !user.IsPlatformAdmin)
            return Result.Fail<PlatformSessionResponse>("invalid_credentials", "Invalid email or password.");

        var now = _clock.UtcNow;
        if (user.IsLockedOut(now))
            return Result.Fail<PlatformSessionResponse>("locked_out", "Account temporarily locked. Try again later.");

        if (!_passwordHasher.Verify(user.PasswordHash, request.Password))
        {
            user.AccessFailedCount++;
            if (user.AccessFailedCount >= _options.MaxFailedAccessAttempts)
            {
                user.LockoutUntil = now.AddMinutes(_options.LockoutMinutes);
                user.AccessFailedCount = 0;
            }
            await _uow.SaveChangesAsync(ct);
            return Result.Fail<PlatformSessionResponse>("invalid_credentials", "Invalid email or password.");
        }

        user.AccessFailedCount = 0;
        user.LockoutUntil = null;
        user.LastLoginAt = now;
        if (_passwordHasher.ShouldRehash(user.PasswordHash))
            user.PasswordHash = _passwordHasher.Hash(request.Password);

        var access = _tokenIssuer.Issue(user.Id, user.Email, null, null, new HashSet<string>(), AppClaims.ScopePlatform);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Platform admin session issued for {Email}", user.Email);
        return Result.Ok(new PlatformSessionResponse(
            user.Email, access.Token, access.ExpiresAtUtc, access.ExpiresInSeconds, "Bearer"));
    }

    public async Task<Result<AuthTokensResponse>> RefreshAsync(
        RefreshRequest request, string? clientIp, CancellationToken ct = default)
    {
        var tokenHash = OpaqueToken.Hash(request.RefreshToken);
        var presented = await _refreshTokens.GetByHashAsync(tokenHash, ct);

        if (presented is null)
            return Result.Fail<AuthTokensResponse>("invalid_refresh", "Refresh token is not recognized.");

        // Reuse of an already-rotated token is a theft signal: revoke the whole session lineage.
        if (presented.RevokedAt is not null)
        {
            _logger.LogWarning("Refresh token reuse detected for user {UserId}; revoking the entire token family", presented.UserId);
            await RevokeLineageAsync(presented, ct);
            return Result.Fail<AuthTokensResponse>("invalid_refresh", "Refresh token is no longer valid.");
        }

        if (!presented.IsActive)
            return Result.Fail<AuthTokensResponse>("expired_refresh", "Session expired; please log in again.");

        var user = await _users.GetByIdAsync(presented.UserId, ct);
        var membership = await _memberships.GetAsync(presented.UserId, presented.TenantId, ct);
        if (user is null || membership is null || membership.Status != MembershipStatus.Active)
            return Result.Fail<AuthTokensResponse>("invalid_refresh", "Session is no longer valid.");

        var response = await IssueSessionAsync(user, presented.TenantId, membership.Role, clientIp, ct, rotateFrom: presented);
        await _uow.SaveChangesAsync(ct);
        return Result.Ok(response);
    }

    public async Task<Result> LogoutAsync(string refreshToken, CancellationToken ct = default)
    {
        var tokenHash = OpaqueToken.Hash(refreshToken);
        var token = await _refreshTokens.GetByHashAsync(tokenHash, ct);
        if (token is { RevokedAt: null })
        {
            token.RevokedAt = _clock.UtcNow;
            _refreshTokens.Update(token);
            await _uow.SaveChangesAsync(ct);
        }
        return Result.Ok();
    }

    public async Task<Result<CurrentUserResponse>> GetCurrentUserAsync(
        Guid userId, Guid tenantId, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct);
        var tenant = await _tenants.GetByIdAsync(tenantId, ct);
        var membership = await _memberships.GetAsync(userId, tenantId, ct);

        if (user is null || tenant is null || membership is null)
            return Result.Fail<CurrentUserResponse>("not_found", "Session context not found.");

        var permissions = RolePermissions.For(membership.Role);
        return Result.Ok(new CurrentUserResponse(
            user.Id, user.Email, tenant.Id, tenant.Name, membership.Role.ToString(), permissions, user.IsPlatformAdmin));
    }

    /// <summary>
    /// Revokes the entire refresh-token family when reuse is detected. Rotation is linear: each
    /// token records the hash of the token that replaced it (<see cref="RefreshToken.ReplacedByTokenHash"/>),
    /// so walking that chain forward from the reused token reaches the live leaf the real user still
    /// holds. Tokens BEFORE the reused one are already revoked (that is how reuse was detected), so
    /// revoking every not-yet-revoked descendant kills the whole family — a stolen token replayed
    /// after rotation logs out the legitimate session too, forcing re-authentication.
    /// </summary>
    private async Task RevokeLineageAsync(RefreshToken reused, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var current = reused;
        // Guard bounds the walk against a corrupt/self-referential chain; normal families are short.
        for (var depth = 0; depth < 128 && current.ReplacedByTokenHash is not null; depth++)
        {
            var next = await _refreshTokens.GetByHashAsync(current.ReplacedByTokenHash, ct);
            if (next is null) break;
            if (next.RevokedAt is null)
            {
                next.RevokedAt = now;
                _refreshTokens.Update(next);
            }
            current = next;
        }
        await _uow.SaveChangesAsync(ct);
    }

    private async Task<AuthTokensResponse> IssueSessionAsync(
        User user, Guid tenantId, UserRole role, string? clientIp, CancellationToken ct,
        RefreshToken? rotateFrom = null)
    {
        var permissions = RolePermissions.For(role);
        var access = _tokenIssuer.Issue(user.Id, user.Email, tenantId, role, permissions);

        var rawRefresh = OpaqueToken.Generate();
        await _refreshTokens.AddAsync(new RefreshToken
        {
            TenantId = tenantId,
            UserId = user.Id,
            TokenHash = OpaqueToken.Hash(rawRefresh),
            ExpiresAt = _clock.UtcNow.AddDays(_options.RefreshTokenDays),
            CreatedByIp = clientIp,
            CreatedAt = _clock.UtcNow
        }, ct);

        if (rotateFrom is not null)
        {
            rotateFrom.RevokedAt = _clock.UtcNow;
            rotateFrom.ReplacedByTokenHash = OpaqueToken.Hash(rawRefresh);
            _refreshTokens.Update(rotateFrom);
        }

        return new AuthTokensResponse(
            access.Token, rawRefresh, access.ExpiresAtUtc, access.ExpiresInSeconds, "Bearer");
    }

    private static string Normalize(string value) => value.Trim().ToLowerInvariant();
}
