using Microsoft.Extensions.DependencyInjection;

namespace SwiftBets.Compliance.Application;

public static class ApplicationRegistration
{
    public static IServiceCollection AddComplianceApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ComplianceHandler>();
        return services;
    }
}
