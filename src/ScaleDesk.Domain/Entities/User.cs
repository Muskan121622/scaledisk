using ScaleDesk.Domain.Common;

namespace ScaleDesk.Domain.Entities;

/// <summary>
/// Global identity. A single User may hold memberships in multiple Tenants.
/// Platform-level: NOT tenant-owned. The password hash is never serialized to DTOs.
/// </summary>
public class User : BaseEntity
{
    public string Email { get; set; } = default!;

    public string PasswordHash { get; set; } = default!;

    public string FirstName { get; set; } = default!;

    public string LastName { get; set; } = default!;

    public bool EmailConfirmed { get; set; }

    /// <summary>
    /// ScaleDesk HQ identity (platform-level, never tenant-scoped). Grants access to /api/platform
    /// only — the request carries no tenant claim, so tenant guards reject it everywhere else.
    /// </summary>
    public bool IsPlatformAdmin { get; set; }

    /// <summary>Progressive lockout state (defends against credential stuffing).</summary>
    public int AccessFailedCount { get; set; }

    public DateTime? LockoutUntil { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public ICollection<Membership> Memberships { get; set; } = new List<Membership>();

    public bool IsLockedOut(DateTime utcNow) => LockoutUntil.HasValue && LockoutUntil.Value > utcNow;
}
