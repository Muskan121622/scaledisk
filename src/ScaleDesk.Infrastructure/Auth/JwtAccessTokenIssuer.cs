using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ScaleDesk.Application.Abstractions;
using ScaleDesk.Application.Constants;
using ScaleDesk.Application.Options;
using ScaleDesk.Domain.Enums;

namespace ScaleDesk.Infrastructure.Auth;

/// <summary>
/// Issues short-lived HMAC-SHA256 signed JWT access tokens embedding the active tenant, role and
/// flattened permission set. Because the tenant id lives in the token, the request pipeline can bind
/// the tenant before any data access happens.
/// </summary>
public sealed class JwtAccessTokenIssuer : IAccessTokenIssuer
{
    private readonly JwtOptions _jwt;
    private readonly AuthOptions _auth;
    private readonly SymmetricSecurityKey _key;

    public JwtAccessTokenIssuer(JwtOptions jwt, AuthOptions auth)
    {
        _jwt = jwt;
        _auth = auth;

        if (string.IsNullOrWhiteSpace(jwt.SecretKey) || Encoding.UTF8.GetByteCount(jwt.SecretKey) < 32)
            throw new InvalidOperationException(
                "Jwt:SecretKey must be provided and at least 32 bytes. Configure it via environment or user-secrets.");

        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SecretKey));
    }

    public IssuedAccessToken Issue(Guid userId, string email, Guid? tenantId, UserRole? role,
        IReadOnlySet<string> permissions, string scope = AppClaims.ScopeStaff, Guid? customerId = null)
    {
        var now = DateTime.UtcNow;
        var lifetime = scope == AppClaims.ScopePlatform ? _auth.PlatformAccessTokenMinutes : _auth.AccessTokenMinutes;
        var expires = now.AddMinutes(lifetime);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(AppClaims.Email, email),
            new(AppClaims.Scope, scope),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        // Staff tokens carry tid + role; platform tokens deliberately carry neither, so every
        // tenant-bound endpoint rejects them via the scope guard without per-endpoint checks.
        if (tenantId.HasValue) claims.Add(new Claim(AppClaims.TenantId, tenantId.Value.ToString()));
        if (role.HasValue) claims.Add(new Claim(AppClaims.Role, role.Value.ToString()));
        if (customerId.HasValue) claims.Add(new Claim(AppClaims.CustomerId, customerId.Value.ToString()));
        claims.AddRange(permissions.Select(p => new Claim(AppClaims.Permission, p)));

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _jwt.Issuer,
            Audience = _jwt.Audience,
            IssuedAt = now,
            Expires = expires,
            SigningCredentials = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256)
        };

        var handler = new JsonWebTokenHandler();
        var token = handler.CreateToken(descriptor);

        return new IssuedAccessToken(token, expires, lifetime * 60);
    }
}
