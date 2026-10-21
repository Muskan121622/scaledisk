using System.Net;
using System.Net.Http.Json;

namespace ScaleDesk.IntegrationTests;

/// <summary>
/// Smoke tests for the HTTP surface: routing, the auth gate, and the OpenAPI document. These prove
/// the pipeline is wired correctly end-to-end without depending on a live database.
/// </summary>
public class ApiSmokeTests(ScaleDeskApiFactory factory) : IClassFixture<ScaleDeskApiFactory>
{
    [Fact]
    public async Task Liveness_probe_returns_ok()
    {
        var response = await factory.CreateClient().GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/tickets")]
    [InlineData("/api/auth/me")]
    [InlineData("/api/customers")]
    [InlineData("/api/teams")]
    [InlineData("/api/sla-policies")]
    [InlineData("/api/invitations/pending")]
    public async Task Protected_endpoints_reject_anonymous(string path)
    {
        var response = await factory.CreateClient().GetAsync(path);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Invitation_accept_is_anonymous_reachable()
    {
        // Accept is [AllowAnonymous]: it must get past auth and fail later (bad token -> 400),
        // proving the route is not gated behind a JWT like the rest of the invitations surface.
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/invitations/accept",
            new { token = "bogus", firstName = "A", lastName = "B", password = "Passw0rd!" });
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Openapi_document_is_served()
    {
        var response = await factory.CreateClient().GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Every_response_carries_baseline_security_headers()
    {
        // Even the anonymous 401 must be hardened: security middleware sits above auth + handlers.
        var response = await factory.CreateClient().GetAsync("/api/tickets");
        AssertHeader(response, "X-Content-Type-Options", "nosniff");
        AssertHeader(response, "X-Frame-Options", "DENY");
        AssertHeader(response, "Referrer-Policy", "no-referrer");
    }

    [Fact]
    public async Task Inbound_correlation_id_is_echoed_back()
    {
        var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("X-Correlation-Id", "trace-abc-123");

        var response = await client.SendAsync(request);

        AssertHeader(response, "X-Correlation-Id", "trace-abc-123");
    }

    [Fact]
    public async Task Correlation_id_is_generated_when_absent()
    {
        var response = await factory.CreateClient().GetAsync("/health/live");
        Assert.True(response.Headers.Contains("X-Correlation-Id"), "expected a generated correlation id");
    }

    private static void AssertHeader(HttpResponseMessage response, string name, string expected)
    {
        Assert.True(response.Headers.TryGetValues(name, out var values), $"missing header {name}");
        Assert.Equal(expected, string.Join(",", values));
    }
}
