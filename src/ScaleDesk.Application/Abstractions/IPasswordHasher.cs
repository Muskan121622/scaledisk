namespace ScaleDesk.Application.Abstractions;

/// <summary>
/// Password hashing contract. The infrastructure implementation wraps ASP.NET Core's
/// <c>PasswordHasher</c> (PBKDF2-HMAC-SHA512) so we never hand-roll crypto and Application
/// stays independent of any web framework.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string hash, string password);

    /// <summary>True when the stored hash uses an outdated algorithm and should be re-hashed on next login.</summary>
    bool ShouldRehash(string hash);
}
