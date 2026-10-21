namespace ScaleDesk.Application.Options;

/// <summary>
/// Authentication tuning bound from configuration. Kept in Application so both the issuer
/// (Infrastructure) and services can reason about lifetimes without magic numbers.
/// </summary>
public sealed record AuthOptions
{
    public const string SectionName = "Authentication";

    /// <summary>Access token lifetime in minutes (short-lived by design).</summary>
    public int AccessTokenMinutes { get; init; } = 15;

    /// <summary>Refresh token lifetime in days.</summary>
    public int RefreshTokenDays { get; init; } = 7;

    /// <summary>
    /// Platform-admin (ScaleDesk HQ) access-token lifetime. Platform sessions are access-only with no
    /// refresh lineage, so they get a longer but still bounded window instead of the 15-minute staff life.
    /// </summary>
    public int PlatformAccessTokenMinutes { get; init; } = 240;

    /// <summary>How long a sent invitation link stays acceptable before it must be re-invited.</summary>
    public int InvitationExpirationDays { get; init; } = 7;

    /// <summary>Failed logins before the account is temporarily locked.</summary>
    public int MaxFailedAccessAttempts { get; init; } = 5;

    public int LockoutMinutes { get; init; } = 15;
}
