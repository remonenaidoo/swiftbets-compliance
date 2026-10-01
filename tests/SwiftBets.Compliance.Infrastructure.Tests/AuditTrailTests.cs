using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;
using SwiftBets.BuildingBlocks.Persistence;
using SwiftBets.BuildingBlocks.Testing;
using SwiftBets.Compliance.Application.Audit;
using SwiftBets.Compliance.Domain;
using SwiftBets.Compliance.Domain.Audit;
using SwiftBets.Compliance.Infrastructure.Audit;
using SwiftBets.BuildingBlocks.Messaging;
using SwiftBets.Contracts.Audit;
using SwiftBets.Contracts.Messaging;
using SwiftBets.Contracts.Serialization;

namespace SwiftBets.Compliance.Infrastructure.Tests;

public sealed class AuditTrailTests(SqlServerFixture sql)
{
    private static AuditRecord Record(string action, string subject = "u-1") =>
        new(Guid.NewGuid(), "wallet", "operator-7", action, "user", subject, null, """{"x":1}""", "corr", DateTimeOffset.UtcNow);

    private static async Task<(ComplianceDatabase Db, AuditTrailHandler Trail)> TrailAsync(SqlServerFixture sql)
    {
        var db = await ComplianceDatabase.CreateAsync(sql);
        return (db, new AuditTrailHandler(new SqlAuditTrail(new SqlServerConnectionFactory(db.ConnectionString), db.Time)));
    }

    [Fact]
    public async Task Entries_chain_in_order_a_redelivery_is_stored_once_and_the_chain_verifies()
    {
        var (_, trail) = await TrailAsync(sql);
        var first = Record("limit.set");

        (await trail.AppendAsync(first, CancellationToken.None)).ShouldBeTrue();
        (await trail.AppendAsync(Record("limit.removed"), CancellationToken.None)).ShouldBeTrue();
        (await trail.AppendAsync(first, CancellationToken.None)).ShouldBeFalse();

        (await trail.VerifyAsync(CancellationToken.None)).ShouldBe(new ChainVerification(true, 2, null));
        (await trail.QueryAsync("user", "u-1", 10, CancellationToken.None)).Select(e => e.Record.Action).ShouldBe(["limit.removed", "limit.set"]);
    }

    [Fact]
    public async Task Concurrent_appends_from_many_partitions_form_one_valid_chain()
    {
        var (_, trail) = await TrailAsync(sql);

        await Task.WhenAll(Enumerable.Range(0, 30).Select(i => trail.AppendAsync(Record($"action-{i}", $"u-{i % 4}"), CancellationToken.None)));

        (await trail.VerifyAsync(CancellationToken.None)).ShouldBe(new ChainVerification(true, 30, null));
    }

    [Fact]
    public async Task Editing_a_stored_entry_is_detected_where_it_happened()
    {
        var (db, trail) = await TrailAsync(sql);
        for (var i = 0; i < 3; i++)
        {
            await trail.AppendAsync(Record($"a{i}"), CancellationToken.None);
        }

        await using (var connection = new SqlConnection(db.ConnectionString))
        {
            await connection.ExecuteAsync("UPDATE compliance.AuditEntries SET Actor = 'cover-up' WHERE Sequence = 2");
        }

        (await trail.VerifyAsync(CancellationToken.None)).ShouldBe(new ChainVerification(false, 1, 2));
    }

    [Fact]
    public async Task Cutting_the_tail_off_is_detected_against_the_head()
    {
        var (db, trail) = await TrailAsync(sql);
        for (var i = 0; i < 3; i++)
        {
            await trail.AppendAsync(Record($"a{i}"), CancellationToken.None);
        }

        await using (var connection = new SqlConnection(db.ConnectionString))
        {
            await connection.ExecuteAsync("DELETE FROM compliance.AuditEntries WHERE Sequence = 3");
        }

        (await trail.VerifyAsync(CancellationToken.None)).ShouldBe(new ChainVerification(false, 2, 3));
    }

    [Fact]
    public async Task A_limit_change_reaches_the_audit_view_through_its_own_event()
    {
        var (db, trail) = await TrailAsync(sql);
        var userId = Guid.NewGuid();
        await db.Handler.SetLimitAsync(userId, LimitKind.Stake, LimitPeriod.Day, 10_000, "ZAR", "self", CancellationToken.None);

        byte[] payload;
        await using (var connection = new SqlConnection(db.ConnectionString))
        {
            payload = await connection.QuerySingleAsync<byte[]>("SELECT Payload FROM outbox.Messages WHERE EventType = 'audit.audit-recorded'");
        }

        var envelope = JsonSerializer.Deserialize<EventEnvelope<AuditRecordedV1>>(payload, ContractJson.Options)!;
        await new AuditRecordedConsumer(trail).HandleAsync(new ConsumedEvent<AuditRecordedV1>(envelope, Topics.AuditRecorded, 0, 0, new Dictionary<string, string>()), CancellationToken.None);

        var entry = (await trail.QueryAsync("user", userId.ToString(), 10, CancellationToken.None)).ShouldHaveSingleItem();
        (entry.Record.Service, entry.Record.Action, entry.Record.Actor).ShouldBe(("compliance", "limit.set", "self"));
        entry.Record.After!.ShouldContain("10000");
        (await trail.VerifyAsync(CancellationToken.None)).Verified.ShouldBeTrue();
    }

    [Fact]
    public async Task The_app_login_cannot_change_or_delete_audit_entries()
    {
        var (db, _) = await TrailAsync(sql);
        await using var connection = new SqlConnection(db.ConnectionString);

        var denied = await connection.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM sys.database_permissions p JOIN sys.database_principals r ON r.principal_id = p.grantee_principal_id " +
            "WHERE r.name = 'swiftbets_app' AND p.state = 'D' AND p.major_id = OBJECT_ID('compliance.AuditEntries') AND p.permission_name IN ('UPDATE', 'DELETE')");

        denied.ShouldBe(2);
    }
}
