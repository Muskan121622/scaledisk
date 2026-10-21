using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using ScaleDesk.Application;
using ScaleDesk.Application.Abstractions;
using ScaleDesk.Application.Abstractions.Repositories;
using ScaleDesk.Application.Options;
using ScaleDesk.Infrastructure.Auth;
using ScaleDesk.Infrastructure.Caching;
using ScaleDesk.Infrastructure.Email;
using ScaleDesk.Infrastructure.Identity;
using ScaleDesk.Infrastructure.Messaging;
using ScaleDesk.Infrastructure.Persistence;
using ScaleDesk.Infrastructure.Persistence.Queries;
using ScaleDesk.Infrastructure.Persistence.Repositories;
using ScaleDesk.Infrastructure.Sla;
using ScaleDesk.Infrastructure.Time;

namespace ScaleDesk.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // --- Options -------------------------------------------------------------
        var authOptions = configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();
        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        var kafkaOptions = configuration.GetSection(KafkaOptions.SectionName).Get<KafkaOptions>() ?? new KafkaOptions();
        var slaOptions = configuration.GetSection(SlaMonitorOptions.SectionName).Get<SlaMonitorOptions>() ?? new SlaMonitorOptions();
        var redisOptions = configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>() ?? new RedisOptions();
        services.AddSingleton(authOptions);
        services.AddSingleton(jwtOptions);
        services.AddSingleton(kafkaOptions);
        services.AddSingleton(slaOptions);
        services.AddSingleton(redisOptions);

        // --- Persistence ---------------------------------------------------------
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres is not configured.");

        services.AddDbContext<ScaleDeskDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.EnableRetryOnFailure(maxRetryCount: 3));
            // Binds app.tenant_id on the Postgres session so row-level security enforces isolation
            // beneath the EF query filter (spec §40). Scoped so it reads the same request's tenant.
            options.AddInterceptors(sp.GetRequiredService<TenantSessionInterceptor>());
        });
        services.AddScoped<TenantSessionInterceptor>();

        // Per-request domain-event buffer drained into the outbox on SaveChanges.
        services.AddScoped<IEventCollector, DomainEventCollector>();

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IMembershipRepository, MembershipRepository>();
        services.AddScoped<IInvitationRepository, InvitationRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ICustomerUserRepository, CustomerUserRepository>();
        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<ITicketRepository, TicketRepository>();
        services.AddScoped<ISlaPolicyRepository, SlaPolicyRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IAutomationRuleRepository, AutomationRuleRepository>();
        services.AddScoped<IPlatformRepository, PlatformRepository>();
        services.AddScoped<DbSeeder>();

        // Read-models for the UI (Stage 5F.1). Kept separate from the write repositories on purpose —
        // these are queries, not domain operations, and are safe to iterate without touching aggregates.
        services.AddScoped<IDashboardQuery, EfDashboardQuery>();

        // --- Cross-cutting services ---------------------------------------------
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordHasher, AspNetPasswordHasher>();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
        services.AddSingleton<IEmailSender, ConsoleEmailSender>();

        // --- Caching (Redis when enabled, in-memory otherwise) -------------------
        // Same IAppCache contract either way, so dev/test run without a Redis server
        // while prod gets a shared, horizontally-consistent cache behind one flag.
        if (redisOptions.Enabled)
        {
            // One shared multiplexer feeds both the distributed cache and the readiness probe.
            // AbortOnConnectFail=false so a cold/absent Redis delays connection rather than crashing
            // startup; the health check surfaces the degraded state instead of a hard boot failure.
            var redisConfig = ConfigurationOptions.Parse(redisOptions.ConnectionString);
            redisConfig.AbortOnConnectFail = false;
            var multiplexer = ConnectionMultiplexer.Connect(redisConfig);
            // Register against the INTERFACE, not the concrete ConnectionMultiplexer: RedisHealthCheck
            // (and any consumer) resolves IConnectionMultiplexer, which would otherwise be unregistered.
            services.AddSingleton<IConnectionMultiplexer>(multiplexer);

            services.AddStackExchangeRedisCache(o =>
            {
                o.ConfigurationOptions = redisConfig;
                o.ConnectionMultiplexerFactory = () => Task.FromResult<IConnectionMultiplexer>(multiplexer);
                o.InstanceName = redisOptions.InstanceName;
            });
        }
        else
            services.AddDistributedMemoryCache();
        services.AddSingleton<IAppCache, DistributedAppCache>();

        // --- Outbox dispatcher (Kafka) ------------------------------------------
        services.AddHostedService<OutboxProcessor>();

        // --- Inbound event processing (idempotent consumer) ---------------------
        // One orchestrator owns dedup + the transaction; handlers plug in and are collected by DI.
        services.AddScoped<IIntegrationEventHandler, NotificationIntegrationEventHandler>();
        services.AddScoped<IIntegrationEventHandler, AutomationIntegrationEventHandler>();
        services.AddScoped<IIntegrationEventProcessor, IntegrationEventProcessor>();
        services.AddHostedService<KafkaEventConsumer>();

        // --- SLA breach scanner --------------------------------------------------
        services.AddHostedService<SlaBreachScanner>();

        // --- Use-cases (Application layer) --------------------------------------
        services.AddApplication();

        return services;
    }
}
