using Dapper;
using Microsoft.Data.SqlClient;
using SwiftBets.BuildingBlocks.Outbox;
using SwiftBets.BuildingBlocks.Persistence;
using SwiftBets.BuildingBlocks.Testing;
using SwiftBets.Compliance.Application;
using SwiftBets.Compliance.Domain;
using SwiftBets.Compliance.Infrastructure.Persistence;
using SwiftBets.Contracts.Messaging;

namespace SwiftBets.Compliance.Infrastructure.Tests;

public sealed class CaseTests(SqlServerFixture sql)
{
    private static async Task<(ComplianceDatabase Db, CaseHandler Cases)> CasesAsync(SqlServerFixture sql)
    {
        var db = await ComplianceDatabase.CreateAsync(sql);
        var outbox = new SqlServerOutbox(Microsoft.Extensions.Options.Options.Create(new BuildingBlocks.Messaging.KafkaOptions { BootstrapServers = "unused:9092", Environment = "test", ClientId = "t" }), db.Time);
        var cases = new SqlCaseStore(new SqlServerConnectionFactory(db.ConnectionString), new AuditWriter(outbox, db.Time, "compliance"));
        return (db, new CaseHandler(db.Store, cases, db.Time));
    }

    [Fact]
    public async Task A_block_is_lifted_only_when_a_second_operator_approves()
    {
        var (db, cases) = await CasesAsync(sql);
        var userId = Guid.NewGuid();
        var added = await cases.AddRestrictionAsync(userId, RestrictionKind.NoBetting, "chargeback review", "ops-1", CancellationToken.None);
        var restrictionId = added.Value.Restrictions.Single().RestrictionId;

        var request = (await cases.RequestLiftAsync(userId, restrictionId, "chargeback resolved", "ops-1", CancellationToken.None)).Value;
        (await cases.RequestLiftAsync(userId, restrictionId, "again", "ops-3", CancellationToken.None)).Error!.Code.ShouldBe("lift_already_requested");
        (await cases.ApproveLiftAsync(userId, request.RequestId, "ops-1", CancellationToken.None)).Error!.Code.ShouldBe("four_eyes_required");
        (await cases.PendingLiftsAsync(userId, CancellationToken.None)).ShouldHaveSingleItem();

        var approved = await cases.ApproveLiftAsync(userId, request.RequestId, "ops-2", CancellationToken.None);

        approved.IsSuccess.ShouldBeTrue();
        (await db.Handler.GetAsync(userId, CancellationToken.None)).Restrictions.ShouldBeEmpty();
        (await cases.PendingLiftsAsync(userId, CancellationToken.None)).ShouldBeEmpty();
        await using var connection = new SqlConnection(db.ConnectionString);
        (await connection.QuerySingleAsync<string>("SELECT LiftedBy FROM compliance.Restrictions WHERE RestrictionId = @Id", new { Id = restrictionId })).ShouldBe("ops-2");
        (await db.OutboxAsync(Topics.AuditRecorded)).Count.ShouldBe(3);
    }

    [Fact]
    public async Task A_self_exclusion_cannot_be_lifted_early()
    {
        var (db, cases) = await CasesAsync(sql);
        var userId = Guid.NewGuid();
        var excluded = await db.Handler.StartExclusionAsync(userId, RestrictionKind.SelfExclusion, null, 6, "", "self", CancellationToken.None);

        var refused = await cases.RequestLiftAsync(userId, excluded.Value.Restrictions.Single().RestrictionId, "customer asked", "ops-1", CancellationToken.None);

        refused.Error!.Code.ShouldBe("exclusion_not_liftable");
    }

    [Fact]
    public async Task Notes_are_kept_newest_first_and_audited()
    {
        var (db, cases) = await CasesAsync(sql);
        var userId = Guid.NewGuid();
        await cases.AddNoteAsync(userId, "called about limits", "ops-1", CancellationToken.None);
        db.Time.Advance(TimeSpan.FromMinutes(1));
        await cases.AddNoteAsync(userId, "sent the help line number", "ops-2", CancellationToken.None);

        (await cases.NotesAsync(userId, CancellationToken.None)).Select(n => n.Author).ShouldBe(["ops-2", "ops-1"]);
        (await cases.AddNoteAsync(userId, "  ", "ops-1", CancellationToken.None)).Error!.Code.ShouldBe("invalid_note");
        (await db.OutboxAsync(Topics.AuditRecorded)).Count.ShouldBe(2);
    }
}
