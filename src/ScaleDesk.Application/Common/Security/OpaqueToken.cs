using System.Security.Cryptography;
using System.Text;

namespace ScaleDesk.Application.Common.Security;

/// <summary>
/// Helpers for opaque refresh tokens: a cryptographically random URL-safe string plus its
/// SHA-256 hex digest. We only ever store the digest, never the raw token, so a database leak
/// does not yield usable sessions.
/// </summary>
public static class OpaqueToken
{
    public static string Generate(int byteLength = 48) =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(byteLength))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    public static string Hash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
