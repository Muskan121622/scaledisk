using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ScaleDesk.Api.Authorization;
using ScaleDesk.Api.Auth;
using ScaleDesk.Api.Health;
using ScaleDesk.Api.Hubs;
using ScaleDesk.Api.Middleware;
using ScaleDesk.Application.Abstractions;
using ScaleDesk.Application.Constants;
using ScaleDesk.Domain.Constants;
using ScaleDesk.Infrastructure;
using ScaleDesk.Infrastructure.Auth;
using ScaleDesk.Infrastructure.Persistence;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using ScaleDesk.Infrastructure.Diagnostics;
using Serilog;
using Serilog.Events;

// --- Structured logging (Serilog) ------------------------------------------
// Replaces the default host logger so every entry is structured and enriched with the ambient
// CorrelationId (pushed by RequestContextMiddleware) plus an Application tag. Console for containers,
// a daily rolling file for local/dev. Sinks are chosen for cost-free, dependency-light readability;
// swapping in a centralised sink (Seq/OTLP) later is a one-line change here, not a codebase sweep.
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "ScaleDesk")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File("logs/scaledesk-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14,
        outputTemplate: "{Timestamp:O} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

// --- Layer wiring -----------------------------------------------------------
builder.Services.AddHttpContextAccessor();
builder.Services.AddInfrastructure(builder.Configuration);

// Per-request ambient context, resolved from the validated JWT's claims.
builder.Services.AddScoped<ICurrentTenant, HttpCurrentTenant>();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

// --- Distributed tracing (OpenTelemetry): one trace spans HTTP -> DB -> outbox -> Kafka -> consumer.
var otelSection = builder.Configuration.GetSection("Otel");
if (otelSection.GetValue("Enabled", false))
{
    var otlpEndpoint = otelSection.GetValue<string>("Endpoint");
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(r => r.AddService("scaledesk-api"))
        .WithTracing(t =>
        {
            t.AddAspNetCoreInstrumentation(o => { o.RecordException = true; o.Filter = c => !c.Request.Path.StartsWithSegments("/health"); })
             .AddHttpClientInstrumentation(o => o.RecordException = true)
             .AddEntityFrameworkCoreInstrumentation()
             .AddSource(ScaledeskActivitySource.Name);
            if (!string.IsNullOrWhiteSpace(otlpEndpoint)) t.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
        });
}


// --- Authentication (JWT bearer) -------------------------------------------
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
          ?? throw new InvalidOperationException("Missing 'Jwt' configuration section.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // keep 'sub' / 'tid' / 'perm' claim types verbatim
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SecretKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "sub",
            RoleClaimType = AppClaims.Role
        };

        // WebSockets can't set an Authorization header, so SignalR clients pass the access token as
        // an "access_token" query string. Read it only for hub paths, leaving REST auth untouched.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken)
                    && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

// --- Real-time (SignalR) ----------------------------------------------------
// Push channel for notifications + live ticket updates (spec §21). Group membership is derived from
// validated JWT claims in the hub, so fan-out stays tenant- and user-scoped. The Application seam
// IRealtimeNotifier is implemented here against the hub, keeping SignalR out of inner layers.
builder.Services.AddSignalR();
builder.Services.AddSingleton<IRealtimeNotifier, SignalRRealtimeNotifier>();

// --- Authorization: one policy per permission token ------------------------
builder.Services.AddAuthorization(options =>
{
    foreach (var permission in Permissions.All)
        options.AddPolicy(permission, policy => policy.Requirements.Add(new PermissionRequirement(permission)));

    // HQ surface: not a permission token — platform admins hold no tenant permissions, so the
    // policy asserts the token's scope claim directly (see PlatformAdminAuthorizationHandler).
    options.AddPolicy(PlatformPolicies.Admin, policy => policy.Requirements.Add(new PlatformAdminRequirement()));
});
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddScoped<IAuthorizationHandler, PlatformAdminAuthorizationHandler>();

// --- MVC, Swagger, health, CORS --------------------------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ScaleDesk API",
        Version = "v1",
        Description = "Production-grade multi-tenant customer support SaaS."
    });
    options.AddSecurityDefinition("bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the access token from /api/auth/login."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "bearer" }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<ScaleDeskDbContext>("database", tags: new[] { "ready" });

// Redis only matters when the distributed cache is on; probe it only then so the in-memory
// fallback path (dev/test) reports a clean readiness with no phantom dependency.
if (builder.Configuration.GetValue("Redis:Enabled", false))
    builder.Services.AddHealthChecks().AddCheck<RedisHealthCheck>("redis", tags: new[] { "ready" });

// --- CORS ------------------------------------------------------------------
// Browsers only. The API authenticates with a bearer token (never cookies), so no credential
// allow-listing is required. Production pins the exact frontend origin(s) via Cors:AllowedOrigins;
// with none configured (local/dev/test) it stays permissive so the app works out of the box.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    if (allowedOrigins.Length > 0)
        policy.WithOrigins(allowedOrigins);
    else
        policy.AllowAnyOrigin();

    policy.AllowAnyHeader().AllowAnyMethod();
}));

// --- Rate limiting (spec §24) ------------------------------------------------
// A broad per-principal ceiling guards every endpoint; a stricter named policy guards the
// credential endpoints against brute-force / stuffing. Partition key is the authenticated user
// when present, else the caller IP, so one noisy client cannot exhaust another's budget.
// Limits are configuration-driven so a dedicated LoadTesting profile can lift them for raw-backend
// measurement (Stage 5D). These defaults reproduce the previous literals exactly (300/min global, 10/min auth).
var rateLimiting = builder.Configuration.GetSection("RateLimiting");
var globalPermit = rateLimiting.GetValue("GlobalPermitPerMinute", 300);
var authPermit = rateLimiting.GetValue("AuthPermitPerMinute", 10);
var rateWindow = TimeSpan.FromSeconds(rateLimiting.GetValue("WindowSeconds", 60));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.ContentType = "application/problem+json";
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            type = "https://scaledesk.dev/errors/rate_limited",
            title = "Too many requests. Please slow down.",
            status = StatusCodes.Status429TooManyRequests
        }, token);
    };

    // Partition by authenticated user when present, else caller IP, so one noisy client
    // cannot exhaust another's budget.
    static string PrincipalKey(HttpContext http)
        => http.User.FindFirst(AppClaims.Subject)?.Value
           ?? http.Connection.RemoteIpAddress?.ToString()
           ?? "anonymous";

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
        RateLimitPartition.GetFixedWindowLimiter(
            PrincipalKey(http),
            _ => new FixedWindowRateLimiterOptions { PermitLimit = globalPermit, Window = rateWindow, QueueLimit = 0 }));

    // Credential endpoints: far tighter, keyed purely by IP (unauthenticated by definition).
    options.AddPolicy("auth", http =>
        RateLimitPartition.GetFixedWindowLimiter(
            http.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = authPermit, Window = rateWindow, QueueLimit = 0 }));
});

var app = builder.Build();

// --- Migrate + seed ---------------------------------------------------------
if (builder.Configuration.GetValue("Database:AutoMigrate", app.Environment.IsDevelopment()))
{
    // Migrations are DDL (CREATE/ALTER TABLE, RLS policies, roles) and must run as the OWNER role.
    // The runtime DbContext is intentionally bound to the least-privilege app role so PostgreSQL
    // Row-Level Security actually applies (a superuser/owner would bypass it) — and that role cannot
    // run DDL. So migrate on a separate owner connection. PostgresMigrator falls back to Postgres
    // when a single privileged account is used (e.g. a bare local run), keeping dev frictionless.
    var ownerConnectionString = builder.Configuration.GetConnectionString("PostgresMigrator")
        ?? builder.Configuration.GetConnectionString("Postgres");

    using (var migrateScope = app.Services.CreateScope())
    {
        var db = migrateScope.ServiceProvider.GetRequiredService<ScaleDeskDbContext>();
        db.Database.SetConnectionString(ownerConnectionString);
        await db.Database.MigrateAsync();
    }

    // Seed uses the runtime (app-role) connection with no tenant bound, so the tenant-session
    // interceptor enables the cross-tenant bypass and the seed rows satisfy the RLS WITH CHECK.
    using (var seedScope = app.Services.CreateScope())
    {
        await seedScope.ServiceProvider.GetRequiredService<DbSeeder>().SeedAsync();
    }
}

// --- Pipeline ---------------------------------------------------------------
// Outermost: assign the correlation id + emit the request completion log, so they wrap everything
// (including the exception handler, whose logs and ProblemDetails payload then carry the id).
app.UseMiddleware<RequestContextMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "ScaleDesk v1"));

app.UseCors();
app.UseAuthentication();
// Scope->URL choke point: must sit after authentication (reads claims) and before authorization
// (rejects before any handler runs).
app.UseMiddleware<TenantScopeGuardMiddleware>();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();
app.MapHub<NotificationsHub>("/hubs/notifications");
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });
app.MapGet("/", () => Results.Redirect("/swagger"));

app.Run();

Log.CloseAndFlush();

/// <summary>Exposed for WebApplicationFactory in the integration test project.</summary>
public partial class Program;
