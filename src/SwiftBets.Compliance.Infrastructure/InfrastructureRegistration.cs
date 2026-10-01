using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SwiftBets.BuildingBlocks.Messaging;
using SwiftBets.BuildingBlocks.Outbox;
using SwiftBets.BuildingBlocks.Persistence;
using SwiftBets.Compliance.Application.Audit;
using SwiftBets.Compliance.Application.Ports;
using SwiftBets.Compliance.Infrastructure.Audit;
using SwiftBets.Contracts.Audit;
using SwiftBets.Contracts.Messaging;
using SwiftBets.Compliance.Infrastructure.Persistence;

namespace SwiftBets.Compliance.Infrastructure;

public static class InfrastructureRegistration
{
    public static IServiceCollection AddComplianceInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSqlServerPersistence(Required(configuration, "ConnectionStrings:SbCompliance"));
        services.AddSingleton<IComplianceStore, SqlComplianceStore>();
        services.AddSingleton<IAuditTrail, SqlAuditTrail>();

        // Events, snapshots and audit entries leave through the outbox; the relay runs in every replica.
        services.AddKafkaMessaging(configuration);
        services.AddSqlServerOutbox(configuration, runRelay: configuration.GetValue("Outbox:RunRelay", true));
        services.AddAuditWriter("compliance");
        if (configuration.GetValue("Audit:Consume", true))
        {
            services.AddKafkaConsumer<AuditRecordedV1, AuditRecordedConsumer>(Topics.AuditRecorded, "compliance.audit");
        }
        services.AddSingleton(TimeProvider.System);
        return services;
    }

    private static string Required(IConfiguration configuration, string key) =>
        configuration[key] is { Length: > 0 } value ? value : throw new InvalidOperationException($"Configuration '{key}' is required.");
}
