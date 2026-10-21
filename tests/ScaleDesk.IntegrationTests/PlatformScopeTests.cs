using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ScaleDesk.IntegrationTests;

/// <summary>
/// Scope enforcement (TenantScopeGuardMiddleware + platform policy) proven without a database:
/// every case is decided before a controller touches EF, using tokens signed with the fixed
/// test key from <see cref="ScaleDeskApiFactory"/>.
/// </summary>
public class PlatformScopeTests(ScaleDeskApiFactory factory) : IClassFixture<ScaleDeskApiFactory>
{
    private const string Secret = "integration-test-only-secret-key-32bytes-min!!";

    [Fact]
    public async Task Platform_endpoints_reject_anonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/platform/stats");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Staff_token_cannot_reach_platform_surface()
    {
        var client = Authed(Token(("sub", Guid.NewGuid().ToString()), ("tid", Guid.NewGuid().ToString()), ("role", "0"), ("scope", "staff")));
        var response = await client.GetAsync("/api/platform/stats");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Legacy_token_without_scope_is_treated_as_staff_and_blocked_from_platform()
    {
        // Tokens minted before scopes existed carry tid but no scope claim — must not gain HQ access.
        var client = Authed(Token(("sub", Guid.NewGuid().ToString()), ("tid", Guid.NewGuid().ToString()), ("role", "0")));
        var response = await client.GetAsync("/api/platform/stats");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Platform_token_cannot_reach_staff_surface()
    {
        // No tid by design; without the guard the permissive null-tenant filter would open every tenant.
        var client = Authed(Token(("sub", Guid.NewGuid().ToString()), ("scope", "platform")));
        var response = await client.GetAsync("/api/tickets");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Portal_token_is_confined_to_portal_surface()
    {
        var client = Authed(Token(("sub", Guid.NewGuid().ToString()), ("scope", "portal"), ("cid", Guid.NewGuid().ToString())));
        var tickets = await client.GetAsync("/api/tickets");
        Assert.Equal(HttpStatusCode.Forbidden, tickets.StatusCode);
    }

    [Fact]
    public async Task Staff_token_cannot_reach_portal_surface()
    {
        var client = Authed(Token(("sub", Guid.NewGuid().ToString()), ("tid", Guid.NewGuid().ToString()), ("role", "0"), ("scope", "staff")));
        var response = await client.GetAsync("/api/portal/tickets");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Platform_token_cannot_reach_portal_surface()
    {
        var client = Authed(Token(("sub", Guid.NewGuid().ToString()), ("scope", "platform")));
        var response = await client.GetAsync("/api/portal/tickets");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Portal_token_cannot_reach_platform_surface()
    {
        var client = Authed(Token(("sub", Guid.NewGuid().ToString()), ("scope", "portal"), ("cid", Guid.NewGuid().ToString())));
        var response = await client.GetAsync("/api/platform/stats");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Portal_token_cannot_reach_staff_auth_surface()
    {
        // /api/auth/* is staff space: a portal token must not call /me or /refresh there.
        var client = Authed(Token(("sub", Guid.NewGuid().ToString()), ("scope", "portal"), ("cid", Guid.NewGuid().ToString())));
        var response = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Staff_token_without_tenant_claim_is_rejected()
    {
        // A staff-scope token must always be tenant-bound; a missing tid means tampering or a bug.
        var client = Authed(Token(("sub", Guid.NewGuid().ToString()), ("scope", "staff")));
        var response = await client.GetAsync("/api/tickets");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Token_signed_with_wrong_key_never_authenticates()
    {
        var client = Authed(Token(new[] { ("sub", Guid.NewGuid().ToString()), ("scope", "platform") }, "another-secret-key-not-used-by-the-host-32bytes!!"));
        var response = await client.GetAsync("/api/platform/stats");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private HttpClient Authed(string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>Minimal HS256 JWT builder — claims go in verbatim (the host sets MapInboundClaims=false).</summary>
    private static string Token(params (string Type, string Value)[] claims) => Token(claims, Secret);

    private static string Token((string Type, string Value)[] claims, string signingKey)
    {
        var payload = new Dictionary<string, string>
        {
            ["iss"] = "scaledesk",
            ["aud"] = "scaledesk-clients",
            ["iat"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
            ["exp"] = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds().ToString()
        };
        foreach (var (type, value) in claims) payload[type] = value;

        var header = B64(JsonSerializer.SerializeToUtf8Bytes(new { alg = "HS256", typ = "JWT" }));
        var body = B64(JsonSerializer.SerializeToUtf8Bytes(payload));
        var signingInput = $"{header}.{body}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(signingKey));
        return $"{signingInput}.{B64(hmac.ComputeHash(Encoding.UTF8.GetBytes(signingInput)))}";
    }

    private static string B64(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
