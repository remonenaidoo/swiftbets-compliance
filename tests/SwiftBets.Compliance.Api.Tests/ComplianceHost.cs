extern alias migrator;

using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using SwiftBets.BuildingBlocks.Outbox;
using SwiftBets.BuildingBlocks.Persistence;
using SwiftBets.BuildingBlocks.Testing;

[assembly: AssemblyFixture(typeof(SqlServerFixture))]

namespace SwiftBets.Compliance.Api.Tests;

/// <summary>The compliance host on a freshly migrated database, accepting tokens from <see cref="TestJwt"/>.</summary>
public sealed class ComplianceHost : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    private ComplianceHost(string connectionString) => _connectionString = connectionString;

    public static async Task<ComplianceHost> StartAsync(SqlServerFixture sql)
    {
        var connectionString = await sql.CreateDatabaseAsync("cmh_" + Guid.NewGuid().ToString("N")[..10]);
        MigrationRunner.RunSqlServer(connectionString, false, OutboxRegistration.Migrations, new MigrationSource(typeof(migrator::Program).Assembly, 1)).Successful.ShouldBeTrue();
        return new ComplianceHost(connectionString);
    }

    public HttpClient ClientFor(string subject)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.Issue(subject));
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:SbCompliance", _connectionString);
        builder.UseSetting("Jwt:Authority", TestJwt.Issuer);
        builder.UseSetting("Jwt:Audience", TestJwt.Audience);
        builder.UseSetting("Jwt:RequireHttpsMetadata", "false");
        builder.UseSetting("Kafka:BootstrapServers", "127.0.0.1:9");
        builder.UseSetting("Kafka:Environment", "test");
        builder.UseSetting("Kafka:ClientId", "compliance-tests");
        builder.UseSetting("Outbox:RunRelay", "false");
        builder.ConfigureTestServices(services => services.UseTestJwt());
    }
}
