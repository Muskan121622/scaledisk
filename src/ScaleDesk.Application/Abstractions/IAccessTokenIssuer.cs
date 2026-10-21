using ScaleDesk.Domain.Enums;

namespace ScaleDesk.Application.Abstractions;

/// <summary>
/// Issues short-lived signed access tokens. Implemented in Infrastructure with JWT so the
/// Application layer has no dependency on any token library.
/// </summary>
public interface IAccessTokenIssuer
{
    /// <summary>
    /// Issues a scoped access token. Staff sessions pass tenant + role + permissions; platform
    /// (HQ) sessions pass neither tenant nor role (tenant guards then reject them on /api/* except
    /// /api/platform); portal (customer) sessions add the linked customer id.
    /// </summary>
    IssuedAccessToken Issue(
        Guid userId, string email, Guid? tenantId, UserRole? role, IReadOnlySet<string> permissions,
        string scope = Constants.AppClaims.ScopeStaff, Guid? customerId = null);
}

public readonly record struct IssuedAccessToken(string Token, DateTime ExpiresAtUtc, int ExpiresInSeconds);
