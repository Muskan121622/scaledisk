using Microsoft.AspNetCore.Identity;
using ScaleDesk.Application.Abstractions;
using ScaleDesk.Domain.Entities;

namespace ScaleDesk.Infrastructure.Identity;

/// <summary>
/// Adapts ASP.NET Core's battle-tested <see cref="PasswordHasher{TUser}"/> (PBKDF2-HMAC-SHA512,
/// per-hash salt) to our framework-free contract. We deliberately do NOT roll our own crypto.
/// </summary>
public sealed class AspNetPasswordHasher : IPasswordHasher
{
    private static readonly PasswordHasher<User> Hasher = new();

    public string Hash(string password) => Hasher.HashPassword(new User(), password);

    public bool Verify(string hash, string password)
    {
        var result = Hasher.VerifyHashedPassword(new User(), hash, password);
        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }

    // Current Identity hashes carry the version marker "AQ"; anything else predates our
    // parameters and is transparently upgraded on next successful login.
    public bool ShouldRehash(string hash) =>
        string.IsNullOrEmpty(hash) || !hash.StartsWith("AQ", StringComparison.Ordinal);
}
