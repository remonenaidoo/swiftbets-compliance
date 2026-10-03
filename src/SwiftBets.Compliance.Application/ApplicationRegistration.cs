using Microsoft.Extensions.DependencyInjection;

namespace SwiftBets.Compliance.Application;

public static class ApplicationRegistration
{
    public static IServiceCollection AddComplianceApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ComplianceHandler>();
        services.AddSingleton<CaseHandler>();
        services.AddSingleton<KycHandler>();
        services.AddSingleton<KycReviewHandler>();
        services.AddSingleton<Audit.AuditTrailHandler>();
        return services;
    }
}
