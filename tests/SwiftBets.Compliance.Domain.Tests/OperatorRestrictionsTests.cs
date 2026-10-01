namespace SwiftBets.Compliance.Domain.Tests;

public sealed class OperatorRestrictionsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Operator_blocks_part_of_the_account_with_a_reason()
    {
        var (restriction, error) = OperatorRestrictions.Add(RestrictionKind.NoBetting, " chargeback ", [], Now);

        error.ShouldBeNull();
        (restriction!.Kind, restriction.EndsAt, restriction.Reason).ShouldBe((RestrictionKind.NoBetting, (DateTimeOffset?)null, "chargeback"));
    }

    [Theory]
    [InlineData(RestrictionKind.SelfExclusion, "r", "not_an_operator_restriction")]
    [InlineData(RestrictionKind.NoDeposits, " ", "reason_required")]
    public void Bad_restrictions_are_refused(RestrictionKind kind, string reason, string code) =>
        OperatorRestrictions.Add(kind, reason, [], Now).Error!.Code.ShouldBe(code);

    [Fact]
    public void The_same_block_cannot_be_added_twice()
    {
        var (first, _) = OperatorRestrictions.Add(RestrictionKind.NoDeposits, "r", [], Now);

        OperatorRestrictions.Add(RestrictionKind.NoDeposits, "r", [first!], Now).Error!.Code.ShouldBe("already_restricted");
    }

    [Fact]
    public void Exclusions_and_ended_blocks_cannot_be_lifted()
    {
        var exclusion = new Restriction(Guid.NewGuid(), RestrictionKind.SelfExclusion, Now.AddDays(-1), Now.AddMonths(6), "r");
        var ended = new Restriction(Guid.NewGuid(), RestrictionKind.NoBetting, Now.AddDays(-2), Now.AddDays(-1), "r");

        OperatorRestrictions.CanRequestLift(exclusion, "please", Now)!.Code.ShouldBe("exclusion_not_liftable");
        OperatorRestrictions.CanRequestLift(ended, "please", Now)!.Code.ShouldBe("restriction_not_found");
        OperatorRestrictions.CanRequestLift(null, "please", Now)!.Code.ShouldBe("restriction_not_found");
    }

    [Fact]
    public void Whoever_asks_cannot_approve()
    {
        var request = new LiftRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "ops-1", "cleared", Now);

        OperatorRestrictions.CanApprove(request, "OPS-1")!.Code.ShouldBe("four_eyes_required");
        OperatorRestrictions.CanApprove(request, "ops-2").ShouldBeNull();
    }
}
