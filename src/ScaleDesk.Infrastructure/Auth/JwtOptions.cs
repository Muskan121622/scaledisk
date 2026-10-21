namespace ScaleDesk.Infrastructure.Auth;

/// <summary>
/// JWT signing configuration, bound from the "Jwt" configuration section. The secret is supplied
/// via environment/user-secrets — it is never committed (spec §42).
/// </summary>
public sealed record JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "scaledesk";
    public string Audience { get; init; } = "scaledesk-clients";

    /// <summary>HMAC-SHA256 symmetric key. Minimum 32 bytes; required in every environment.</summary>
    public string SecretKey { get; init; } = string.Empty;
}
