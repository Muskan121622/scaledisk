using ScaleDesk.Domain.Common;

namespace ScaleDesk.Domain.Entities;

/// <summary>
/// Opaque refresh token bound to a (user, tenant) session. We persist only a SHA-256 hash,
/// never the raw token. Rotation sets <see cref="ReplacedByTokenHash"/> so reuse of an old
/// token can be detected as a potential theft signal.
/// </summary>
public class RefreshToken : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public Guid UserId { get; set; }

    /// <summary>SHA-256 (hex) of the opaque token handed to the client.</summary>
    public string TokenHash { get; set; } = default!;

    public DateTime ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public string? ReplacedByTokenHash { get; set; }

    public string? CreatedByIp { get; set; }

    public bool IsActive =>
        RevokedAt is null && ExpiresAt > DateTime.UtcNow;
}
