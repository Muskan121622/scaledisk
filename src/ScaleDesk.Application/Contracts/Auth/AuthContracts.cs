namespace ScaleDesk.Application.Contracts.Auth;

public sealed record RegisterRequest(
    string OrganizationName,
    string OrganizationSlug,
    string Email,
    string Password,
    string FirstName,
    string LastName);

public sealed record LoginRequest(
    string Email,
    string Password,
    string OrganizationSlug);

public sealed record RefreshRequest(string RefreshToken);

public sealed record AuthTokensResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAtUtc,
    int ExpiresInSeconds,
    string TokenType);

public sealed record CurrentUserResponse(
    Guid UserId,
    string Email,
    Guid TenantId,
    string TenantName,
    string Role,
    IReadOnlyCollection<string> Permissions,
    /// <summary>Also an HQ account — lets the client offer the "Open HQ" entry without a second login.</summary>
    bool IsPlatformAdmin = false);
