namespace SwiftBets.Compliance.Domain.Tests;

public sealed class ExclusionPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(5)]
    [InlineData(61)]
    [InlineData(null)]
    public void Self_exclusion_outside_6_to_60_months_is_refused(int? months) =>
        ExclusionPolicy.Start(RestrictionKind.SelfExclusion, null, months, "", [], Now).Error!.Code.ShouldBe("invalid_self_exclusion");

    [Fact]
    public void Six_month_self_exclusion_runs_from_now()
    {
        var (restriction, error) = ExclusionPolicy.Start(RestrictionKind.SelfExclusion, null, 6, " ", [], Now);

        error.ShouldBeNull();
        (restriction!.Kind, restriction.StartsAt, restriction.EndsAt, restriction.Reason).ShouldBe((RestrictionKind.SelfExclusion, Now, (DateTimeOffset?)Now.AddMonths(6), "customer request"));
        restriction.IsActive(Now).ShouldBeTrue();
        restriction.IsActive(Now.AddMonths(6)).ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(43)]
    [InlineData(null)]
    public void Cooling_off_outside_1_to_42_days_is_refused(int? days) =>
        ExclusionPolicy.Start(RestrictionKind.CoolingOff, days, null, "", [], Now).Error!.Code.ShouldBe("invalid_cooling_off");

    [Fact]
    public void Only_exclusions_can_be_started_this_way() =>
        ExclusionPolicy.Start(RestrictionKind.NoBetting, 7, null, "", [], Now).Error!.Code.ShouldBe("not_an_exclusion");

    [Fact]
    public void A_shorter_exclusion_cannot_replace_a_longer_one()
    {
        var (sixMonths, _) = ExclusionPolicy.Start(RestrictionKind.SelfExclusion, null, 6, "r", [], Now);

        ExclusionPolicy.Start(RestrictionKind.CoolingOff, 7, null, "r", [sixMonths!], Now).Error!.Code.ShouldBe("already_excluded");
    }

    [Fact]
    public void A_longer_exclusion_may_follow_a_cooling_off()
    {
        var (week, _) = ExclusionPolicy.Start(RestrictionKind.CoolingOff, 7, null, "r", [], Now);

        ExclusionPolicy.Start(RestrictionKind.SelfExclusion, null, 12, "r", [week!], Now).Error.ShouldBeNull();
    }

    [Fact]
    public void An_ended_exclusion_does_not_block_a_new_one()
    {
        var ended = new Restriction(Guid.NewGuid(), RestrictionKind.CoolingOff, Now.AddDays(-10), Now.AddDays(-3), "r");

        ExclusionPolicy.Start(RestrictionKind.CoolingOff, 3, null, "r", [ended], Now).Error.ShouldBeNull();
    }
}
