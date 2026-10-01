using Dapper;
using Microsoft.Data.SqlClient;
using SwiftBets.BuildingBlocks.Testing;
using SwiftBets.Compliance.Domain;
using SwiftBets.Contracts.Messaging;

[assembly: AssemblyFixture(typeof(SqlServerFixture))]

namespace SwiftBets.Compliance.Infrastructure.Tests;

public sealed class ComplianceStoreTests(SqlServerFixture sql)
{
    [Fact]
    public async Task Unknown_account_has_no_limits_or_blocks()
    {
        var db = await ComplianceDatabase.CreateAsync(sql);

        var state = await db.Handler.GetAsync(Guid.NewGuid(), CancellationToken.None);

        (state.Revision, state.Limits.Count, state.Restrictions.Count, state.KycStatus).ShouldBe((0L, 0, 0, KycStatus.NotStarted));
    }

    [Fact]
    public async Task Limit_change_commits_with_its_event_snapshot_and_audit_entry()
    {
        var db = await ComplianceDatabase.CreateAsync(sql);
        var userId = Guid.NewGuid();

        var result = await db.Handler.SetLimitAsync(userId, LimitKind.Deposit, LimitPeriod.Week, 100_000, "ZAR", "self", CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var stored = await db.Handler.GetAsync(userId, CancellationToken.None);
        stored.Revision.ShouldBe(1);
        stored.Limit(LimitKind.Deposit, LimitPeriod.Week)!.Amount.ShouldBe(100_000);
        (await db.OutboxAsync(Topics.LimitChanged)).ShouldBe(["compliance.limit-changed"]);
        (await db.OutboxAsync(Topics.RestrictionsChanged)).ShouldBe(["compliance.restrictions-changed"]);
        (await db.OutboxAsync(Topics.AuditRecorded)).ShouldBe(["audit.audit-recorded"]);
    }

    [Fact]
    public async Task Raise_is_stored_as_pending_and_applies_after_24_hours()
    {
        var db = await ComplianceDatabase.CreateAsync(sql);
        var userId = Guid.NewGuid();
        await db.Handler.SetLimitAsync(userId, LimitKind.Stake, LimitPeriod.Day, 10_000, "ZAR", "self", CancellationToken.None);

        await db.Handler.SetLimitAsync(userId, LimitKind.Stake, LimitPeriod.Day, 50_000, "ZAR", "self", CancellationToken.None);

        var pending = (await db.Handler.GetAsync(userId, CancellationToken.None)).Limit(LimitKind.Stake, LimitPeriod.Day)!;
        (pending.Amount, pending.PendingAmount).ShouldBe((10_000L, (long?)50_000));
        db.Time.Advance(TimeSpan.FromHours(24));
        (await db.Handler.GetAsync(userId, CancellationToken.None)).Limit(LimitKind.Stake, LimitPeriod.Day)!.Amount.ShouldBe(50_000);
    }

    [Fact]
    public async Task Repeating_the_current_limit_writes_nothing()
    {
        var db = await ComplianceDatabase.CreateAsync(sql);
        var userId = Guid.NewGuid();
        await db.Handler.SetLimitAsync(userId, LimitKind.Loss, LimitPeriod.Month, 20_000, "ZAR", "self", CancellationToken.None);

        await db.Handler.SetLimitAsync(userId, LimitKind.Loss, LimitPeriod.Month, 20_000, "ZAR", "self", CancellationToken.None);

        (await db.Handler.GetAsync(userId, CancellationToken.None)).Revision.ShouldBe(1);
        (await db.OutboxAsync(Topics.AuditRecorded)).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Refused_change_leaves_no_trace()
    {
        var db = await ComplianceDatabase.CreateAsync(sql);
        var userId = Guid.NewGuid();
        await db.Handler.SetLimitAsync(userId, LimitKind.Loss, LimitPeriod.Month, 20_000, "ZAR", "self", CancellationToken.None);

        var result = await db.Handler.SetLimitAsync(userId, LimitKind.Loss, LimitPeriod.Month, 10_000, "USD", "self", CancellationToken.None);

        result.Error!.Code.ShouldBe("currency_mismatch");
        (await db.Handler.GetAsync(userId, CancellationToken.None)).Revision.ShouldBe(1);
    }

    [Fact]
    public async Task Self_exclusion_is_stored_and_announced()
    {
        var db = await ComplianceDatabase.CreateAsync(sql);
        var userId = Guid.NewGuid();

        var result = await db.Handler.StartExclusionAsync(userId, RestrictionKind.SelfExclusion, null, 6, "needs a break", "self", CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var state = await db.Handler.GetAsync(userId, CancellationToken.None);
        state.IsExcluded(db.Time.GetUtcNow()).ShouldBeTrue();
        state.Restrictions.ShouldHaveSingleItem().EndsAt.ShouldBe(db.Time.GetUtcNow().AddMonths(6));
        (await db.OutboxAsync(Topics.SelfExclusionStarted)).ShouldBe(["compliance.self-exclusion-started"]);
        (await db.Handler.StartExclusionAsync(userId, RestrictionKind.CoolingOff, 7, null, "", "self", CancellationToken.None)).Error!.Code.ShouldBe("already_excluded");
    }

    [Fact]
    public async Task An_ended_exclusion_is_announced_once_with_a_new_snapshot_and_an_audit_entry()
    {
        var db = await ComplianceDatabase.CreateAsync(sql);
        var userId = Guid.NewGuid();
        await db.Handler.StartExclusionAsync(userId, RestrictionKind.CoolingOff, 1, null, "", "self", CancellationToken.None);
        (await db.Handler.AnnounceEndedExclusionsAsync(CancellationToken.None)).ShouldBe(0);

        db.Time.Advance(TimeSpan.FromDays(1));

        (await db.Handler.AnnounceEndedExclusionsAsync(CancellationToken.None)).ShouldBe(1);
        (await db.Handler.AnnounceEndedExclusionsAsync(CancellationToken.None)).ShouldBe(0);
        var state = await db.Handler.GetAsync(userId, CancellationToken.None);
        (state.Revision, state.IsExcluded(db.Time.GetUtcNow()), state.Restrictions.Count).ShouldBe((2L, false, 0));
        (await db.OutboxAsync(Topics.RestrictionsChanged)).Count.ShouldBe(2);
        (await db.OutboxAsync(Topics.AuditRecorded)).Count.ShouldBe(2);
    }

    [Fact]
    public async Task Concurrent_changes_to_one_account_run_in_turn()
    {
        var db = await ComplianceDatabase.CreateAsync(sql);
        var userId = Guid.NewGuid();

        var results = await Task.WhenAll(Enumerable.Range(1, 20).Select(i =>
            db.Handler.SetLimitAsync(userId, (LimitKind)(i % 3), (LimitPeriod)(i % 3 == 0 ? 0 : i % 2 + 1), 1_000 + i, "ZAR", "self", CancellationToken.None)));

        results.ShouldAllBe(r => r.IsSuccess);
        var revisions = (await db.Handler.GetAsync(userId, CancellationToken.None)).Revision;
        await using var connection = new SqlConnection(db.ConnectionString);
        var snapshots = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM outbox.Messages WHERE EventType = 'compliance.restrictions-changed'");
        revisions.ShouldBe(snapshots);
        revisions.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Rollback_to_1_removes_the_tables_and_migrating_again_restores_them()
    {
        var db = await ComplianceDatabase.CreateAsync(sql);

        (await ComplianceDatabase.MigrateAsync(db.ConnectionString, "--Migrator:RollbackTo=1")).ShouldBe(0);
        await using (var connection = new SqlConnection(db.ConnectionString))
        {
            (await connection.ExecuteScalarAsync<int?>("SELECT OBJECT_ID(N'compliance.Limits')")).ShouldBeNull();
        }

        (await ComplianceDatabase.MigrateAsync(db.ConnectionString)).ShouldBe(0);
        (await db.Handler.SetLimitAsync(Guid.NewGuid(), LimitKind.Deposit, LimitPeriod.Day, 1, "ZAR", "self", CancellationToken.None)).IsSuccess.ShouldBeTrue();
    }
}
