using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using SwiftBets.BuildingBlocks.Messaging;
using SwiftBets.BuildingBlocks.Outbox;
using SwiftBets.BuildingBlocks.Persistence;
using SwiftBets.BuildingBlocks.Testing;
using SwiftBets.Compliance.Application;
using SwiftBets.Compliance.Infrastructure.Persistence;
using SwiftBets.Contracts.Messaging;

namespace SwiftBets.Compliance.Infrastructure.Tests;

/// <summary>A freshly migrated SbCompliance database with the handler wired to the real store, outbox and audit writer.</summary>
public sealed class ComplianceDatabase
{
    private ComplianceDatabase(string connectionString)
    {
        ConnectionString = connectionString;
        var outbox = new SqlServerOutbox(Options.Create(new KafkaOptions { BootstrapServers = "unused:9092", Environment = "test", ClientId = "compliance-tests" }), Time);
        Store = new SqlComplianceStore(new SqlServerConnectionFactory(connectionString), outbox, new AuditWriter(outbox, Time, "compliance"));
        Handler = new ComplianceHandler(Store, Time);
    }

    public string ConnectionString { get; }

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));

    public SqlComplianceStore Store { get; }

    public ComplianceHandler Handler { get; }

    public static async Task<ComplianceDatabase> CreateAsync(SqlServerFixture sql)
    {
        var connectionString = await sql.CreateDatabaseAsync("cmp_" + Guid.NewGuid().ToString("N")[..10]);
        (await MigrateAsync(connectionString)).ShouldBe(0);
        return new ComplianceDatabase(connectionString);
    }

    public static async Task<int> MigrateAsync(string connectionString, params string[] extra)
    {
        var result = typeof(Program).Assembly.EntryPoint!.Invoke(null, [new[] { $"--ConnectionStrings:SbCompliance={connectionString}" }.Concat(extra).ToArray()]);
        return result is Task<int> task ? await task : (int)result!;
    }

    /// <summary>Event types in the outbox for a topic base, oldest first.</summary>
    public async Task<IReadOnlyList<string>> OutboxAsync(string topicBase)
    {
        await using var connection = new SqlConnection(ConnectionString);
        return [.. await connection.QueryAsync<string>("SELECT EventType FROM outbox.Messages WHERE Topic = @Topic ORDER BY Sequence", new { Topic = TopicName.For(topicBase, "test").Value })];
    }
}
