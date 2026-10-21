using Microsoft.Extensions.Logging;
using ScaleDesk.Application.Abstractions;
using ScaleDesk.Application.Abstractions.Repositories;
using ScaleDesk.Application.Common;
using ScaleDesk.Application.Common.Security;
using ScaleDesk.Application.Constants;
using ScaleDesk.Application.Contracts.Auth;
using ScaleDesk.Application.Contracts.Portal;
using ScaleDesk.Application.Options;
using ScaleDesk.Domain.Entities;
using ScaleDesk.Domain.Enums;

namespace ScaleDesk.Application.Services;

/// <summary>
/// Customer portal sessions. Mirrors AuthService's credential discipline (progressive lockout,
/// transparent rehash, refresh rotation with lineage revocation on reuse) but mints a different
/// token shape: scope=portal + cid + tid, no role, no permissions. The tenant comes from the
/// slug in the request; everything downstream trusts only the JWT claims.
/// </summary>
public sealed class PortalService(
    IUserRepository users,
    ITenantRepository tenants,
    ICustomerRepository customers,
    ICustomerUserRepository customerUsers,
    IRefreshTokenRepository refreshTokens,
    IPasswordHasher passwordHasher,
    IAccessTokenIssuer tokenIssuer,
    IUnitOfWork uow,
    IClock clock,
    AuthOptions options,
    ILogger<PortalService> logger) : IPortalService
{
    public async Task<Result<AuthTokensResponse>> RegisterAsync(
        PortalRegisterRequest request, string? clientIp, CancellationToken ct = default)
    {
        var email = Normalize(request.Email);
        var slug = Normalize(request.OrganizationSlug);

        var tenant = await tenants.GetBySlugAsync(slug, ct);
        if (tenant is null)
            return Result.Fail<AuthTokensResponse>("not_found", "Unknown workspace.");
        if (tenant.Status != TenantStatus.Active)
            return Result.Fail<AuthTokensResponse>("tenant_suspended", "This workspace is suspended. Contact support.");

        var user = await users.GetByEmailAsync(email, ct);
        if (user is null)
        {
            user = new User
            {
                Email = email,
                PasswordHash = passwordHasher.Hash(request.Password),
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                EmailConfirmed = false,
                CreatedAt = clock.UtcNow
            };
            await users.AddAsync(user, ct);
        }
        else
        {
            // Link-by-email claim: an existing login (e.g. a seeded demo customer's staff-side
            // account or a previous portal registration) must prove the password first.
            var credential = await VerifyCredentialAsync(user, request.Password, ct);
            if (credential is not null)
                return credential;
        }

        // Reuse the tenant's customer record with this email, or open a new one — same email is
        // how the seeded demo customers "claim" their portal account.
        var customer = await customers.FindByEmailInTenantAsync(tenant.Id, email, ct);
        if (customer is null)
        {
            customer = new Customer
            {
                TenantId = tenant.Id,
                Name = $"{user.FirstName} {user.LastName}",
                Email = email,
                CreatedAt = clock.UtcNow
            };
            await customers.AddAsync(customer, ct);
        }

        if (await customerUsers.GetByUserIdAsync(user.Id, tenant.Id, ct) is null)
            await customerUsers.AddAsync(new CustomerUser
            {
                TenantId = tenant.Id,
                UserId = user.Id,
                CustomerId = customer.Id,
                CreatedAt = clock.UtcNow
            }, ct);

        var tokens = await IssueSessionAsync(user, tenant.Id, customer.Id, clientIp, ct);
        await uow.SaveChangesAsync(ct);

        logger.LogInformation("Portal account registered: user {UserId} linked to customer {CustomerId} in tenant {TenantId}",
            user.Id, customer.Id, tenant.Id);
        return Result.Ok(tokens);
    }

    public async Task<Result<AuthTokensResponse>> LoginAsync(
        PortalLoginRequest request, string? clientIp, CancellationToken ct = default)
    {
        var email = Normalize(request.Email);
        var slug = Normalize(request.OrganizationSlug);

        var user = await users.GetByEmailAsync(email, ct);
        var tenant = await tenants.GetBySlugAsync(slug, ct);

        // Generic failure for unknown user/tenant: no enumeration, same vocabulary as staff login.
        if (user is null || tenant is null)
            return Result.Fail<AuthTokensResponse>("invalid_credentials", "Invalid email or password.");
        if (tenant.Status != TenantStatus.Active)
            return Result.Fail<AuthTokensResponse>("tenant_suspended", "This workspace is suspended. Contact support.");

        var credential = await VerifyCredentialAsync(user, request.Password, ct);
        if (credential is not null)
            return credential;

        var link = await customerUsers.GetByUserIdAsync(user.Id, tenant.Id, ct);
        if (link is null)
            return Result.Fail<AuthTokensResponse>("no_portal_account",
                "No portal account in this workspace yet — use Sign up first.");

        var tokens = await IssueSessionAsync(user, tenant.Id, link.CustomerId, clientIp, ct);
        await uow.SaveChangesAsync(ct);
        return Result.Ok(tokens);
    }

    public async Task<Result<AuthTokensResponse>> RefreshAsync(
        PortalRefreshRequest request, string? clientIp, CancellationToken ct = default)
    {
        var tokenHash = OpaqueToken.Hash(request.RefreshToken);
        var presented = await refreshTokens.GetByHashAsync(tokenHash, ct);

        if (presented is null)
            return Result.Fail<AuthTokensResponse>("invalid_refresh", "Refresh token is not recognized.");

        if (presented.RevokedAt is not null)
        {
            logger.LogWarning("Portal refresh token reuse detected for user {UserId}; revoking the token family", presented.UserId);
            await RevokeLineageAsync(presented, ct);
            return Result.Fail<AuthTokensResponse>("invalid_refresh", "Refresh token is no longer valid.");
        }

        if (!presented.IsActive)
            return Result.Fail<AuthTokensResponse>("expired_refresh", "Session expired; please log in again.");

        var user = await users.GetByIdAsync(presented.UserId, ct);
        var link = user is null
            ? null
            : await customerUsers.GetByUserIdAsync(user.Id, presented.TenantId, ct);
        if (link is null)
            return Result.Fail<AuthTokensResponse>("invalid_refresh", "Session is no longer valid.");

        var access = tokenIssuer.Issue(user!.Id, user.Email, presented.TenantId, null, new HashSet<string>(),
            AppClaims.ScopePortal, link.CustomerId);

        var rawRefresh = OpaqueToken.Generate();
        await refreshTokens.AddAsync(new RefreshToken
        {
            TenantId = presented.TenantId,
            UserId = user.Id,
            TokenHash = OpaqueToken.Hash(rawRefresh),
            ExpiresAt = clock.UtcNow.AddDays(options.RefreshTokenDays),
            CreatedByIp = clientIp,
            CreatedAt = clock.UtcNow
        }, ct);

        presented.RevokedAt = clock.UtcNow;
        presented.ReplacedByTokenHash = OpaqueToken.Hash(rawRefresh);
        refreshTokens.Update(presented);
        await uow.SaveChangesAsync(ct);

        return Result.Ok(new AuthTokensResponse(
            access.Token, rawRefresh, access.ExpiresAtUtc, access.ExpiresInSeconds, "Bearer"));
    }

    public async Task<Result> LogoutAsync(string refreshToken, CancellationToken ct = default)
    {
        var token = await refreshTokens.GetByHashAsync(OpaqueToken.Hash(refreshToken), ct);
        if (token is { RevokedAt: null })
        {
            token.RevokedAt = clock.UtcNow;
            refreshTokens.Update(token);
            await uow.SaveChangesAsync(ct);
        }
        return Result.Ok();
    }

    /// <summary>
    /// Lockout + password check with the staff rules. Returns null when the credential is good
    /// (state cleared, hash maybe upgraded) or the ready-made failure result otherwise.
    /// </summary>
    private async Task<Result<AuthTokensResponse>?> VerifyCredentialAsync(User user, string password, CancellationToken ct)
    {
        var now = clock.UtcNow;
        if (user.IsLockedOut(now))
            return Result.Fail<AuthTokensResponse>("locked_out", "Account temporarily locked. Try again later.");

        if (!passwordHasher.Verify(user.PasswordHash, password))
        {
            user.AccessFailedCount++;
            if (user.AccessFailedCount >= options.MaxFailedAccessAttempts)
            {
                user.LockoutUntil = now.AddMinutes(options.LockoutMinutes);
                user.AccessFailedCount = 0;
            }
            await uow.SaveChangesAsync(ct);
            return Result.Fail<AuthTokensResponse>("invalid_credentials", "Invalid email or password.");
        }

        user.AccessFailedCount = 0;
        user.LockoutUntil = null;
        user.LastLoginAt = now;
        if (passwordHasher.ShouldRehash(user.PasswordHash))
            user.PasswordHash = passwordHasher.Hash(password);
        return null;
    }

    private async Task<AuthTokensResponse> IssueSessionAsync(
        User user, Guid tenantId, Guid customerId, string? clientIp, CancellationToken ct)
    {
        var access = tokenIssuer.Issue(user.Id, user.Email, tenantId, null, new HashSet<string>(),
            AppClaims.ScopePortal, customerId);

        var rawRefresh = OpaqueToken.Generate();
        await refreshTokens.AddAsync(new RefreshToken
        {
            TenantId = tenantId,
            UserId = user.Id,
            TokenHash = OpaqueToken.Hash(rawRefresh),
            ExpiresAt = clock.UtcNow.AddDays(options.RefreshTokenDays),
            CreatedByIp = clientIp,
            CreatedAt = clock.UtcNow
        }, ct);

        return new AuthTokensResponse(
            access.Token, rawRefresh, access.ExpiresAtUtc, access.ExpiresInSeconds, "Bearer");
    }

    /// <summary>Same forward-chain walk as AuthService: a replayed token kills the whole family.</summary>
    private async Task RevokeLineageAsync(RefreshToken reused, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var current = reused;
        for (var depth = 0; depth < 128 && current.ReplacedByTokenHash is not null; depth++)
        {
            var next = await refreshTokens.GetByHashAsync(current.ReplacedByTokenHash, ct);
            if (next is null) break;
            if (next.RevokedAt is null)
            {
                next.RevokedAt = now;
                refreshTokens.Update(next);
            }
            current = next;
        }
        await uow.SaveChangesAsync(ct);
    }

    private static string Normalize(string value) => value.Trim().ToLowerInvariant();
}
