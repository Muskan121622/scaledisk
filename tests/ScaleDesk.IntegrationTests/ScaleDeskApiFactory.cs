using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ScaleDesk.IntegrationTests;

/// <summary>
/// Boots the real API host with test configuration: migrations/seed disabled and a fixed JWT key.
/// Configuration is supplied via environment variables because those reliably override the
/// app's own appsettings sources under the minimal-hosting model, letting these tests exercise
/// routing + the authentication pipeline without needing PostgreSQL up.
/// </summary>
public sealed class ScaleDeskApiFactory : WebApplicationFactory<Program>
{
    static ScaleDeskApiFactory()
    {
        Environment.SetEnvironmentVariable("Jwt__SecretKey", "integration-test-only-secret-key-32bytes-min!!");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "scaledesk");
        Environment.SetEnvironmentVariable("Jwt__Audience", "scaledesk-clients");
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", "Host=localhost;Port=5432;Database=test;Username=t;Password=t");
        Environment.SetEnvironmentVariable("Database__AutoMigrate", "false");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
    }
}
