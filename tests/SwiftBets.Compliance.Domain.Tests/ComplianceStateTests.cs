namespace SwiftBets.Compliance.Domain.Tests;

public sealed class ComplianceStateTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Settling_applies_due_changes_and_drops_ended_blocks()
    {
        var state = ComplianceState.New(Guid.NewGuid()) with
        {
            Limits =
            [
                new MoneyLimit(LimitKind.Deposit, LimitPeriod.Week, 1_000, "ZAR", 2_000, Now.AddHours(-1)),
                new MoneyLimit(LimitKind.Loss, LimitPeriod.Month, 5_000, "ZAR", null, Now.AddHours(-1)),
                new MoneyLimit(LimitKind.Stake, LimitPeriod.Day, 500, "ZAR", 900, Now.AddHours(3)),
            ],
            Restrictions =
            [
                new Restriction(Guid.NewGuid(), RestrictionKind.CoolingOff, Now.AddDays(-5), Now.AddDays(-1), "r"),
                new Restriction(Guid.NewGuid(), RestrictionKind.NoMarketing, Now.AddDays(-5), null, "r"),
            ],
        };

        var settled = state.Settled(Now);

        settled.Limit(LimitKind.Deposit, LimitPeriod.Week)!.Amount.ShouldBe(2_000);
        settled.Limit(LimitKind.Loss, LimitPeriod.Month).ShouldBeNull();
        settled.Limit(LimitKind.Stake, LimitPeriod.Day)!.HasPending.ShouldBeTrue();
        settled.Restrictions.ShouldHaveSingleItem().Kind.ShouldBe(RestrictionKind.NoMarketing);
        settled.IsExcluded(Now).ShouldBeFalse();
    }

    [Fact]
    public void Replacing_a_limit_keeps_the_others()
    {
        var state = ComplianceState.New(Guid.NewGuid())
            .WithLimit(LimitKind.Deposit, LimitPeriod.Day, new MoneyLimit(LimitKind.Deposit, LimitPeriod.Day, 100, "ZAR", null, null))
            .WithLimit(LimitKind.Stake, LimitPeriod.Day, new MoneyLimit(LimitKind.Stake, LimitPeriod.Day, 50, "ZAR", null, null))
            .WithLimit(LimitKind.Deposit, LimitPeriod.Day, null);

        state.Limits.ShouldHaveSingleItem().Kind.ShouldBe(LimitKind.Stake);
    }

    [Theory]
    [InlineData(14, null, "invalid_session_limit")]
    [InlineData(1441, null, "invalid_session_limit")]
    [InlineData(null, 9, "invalid_reality_check")]
    [InlineData(null, 241, "invalid_reality_check")]
    public void Session_settings_outside_their_ranges_are_refused(int? session, int? reality, string code) =>
        SessionSettings.Validate(session, reality)!.Code.ShouldBe(code);

    [Fact]
    public void Session_settings_in_range_or_cleared_pass()
    {
        SessionSettings.Validate(60, 30).ShouldBeNull();
        SessionSettings.Validate(null, null).ShouldBeNull();
    }
}
