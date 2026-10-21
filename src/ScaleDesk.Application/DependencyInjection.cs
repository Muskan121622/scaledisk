using Microsoft.Extensions.DependencyInjection;
using ScaleDesk.Application.Services;

namespace ScaleDesk.Application;

public static class DependencyInjection
{
    /// <summary>Registers use-case services. Infrastructure supplies the abstractions they depend on.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IPlatformService, PlatformService>();
        services.AddScoped<IPortalService, PortalService>();
        services.AddScoped<ITicketService, TicketService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<ITeamService, TeamService>();
        services.AddScoped<IInvitationService, InvitationService>();
        services.AddScoped<ISlaService, SlaService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IAutomationService, AutomationService>();
        return services;
    }
}
