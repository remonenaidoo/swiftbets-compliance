namespace SwiftBets.Compliance.Domain.Tests;

public sealed class LimitPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private static MoneyLimit Daily(long amount) => new(LimitKind.Stake, LimitPeriod.Day, amount, "ZAR", null, null);

    [Fact]
    public void First_limit_applies_at_once()
    {
        var (limit, effectiveAt) = LimitPolicy.Change(null, LimitKind.Stake, LimitPeriod.Day, 10_000, "ZAR", Now);

        limit.ShouldBe(Daily(10_000));
        effectiveAt.ShouldBe(Now);
    }

    [Fact]
    public void Lower_limit_applies_at_once_and_cancels_a_pending_raise()
    {
        var pendingRaise = Daily(10_000) with { PendingAmount = 50_000, PendingEffectiveAt = Now.AddHours(10) };

        var (limit, effectiveAt) = LimitPolicy.Change(pendingRaise, LimitKind.Stake, LimitPeriod.Day, 5_000, "ZAR", Now);

        limit.ShouldBe(Daily(5_000));
        effectiveAt.ShouldBe(Now);
    }

    [Fact]
    public void Raise_waits_24_hours_while_the_current_limit_still_applies()
    {
        var (limit, effectiveAt) = LimitPolicy.Change(Daily(10_000), LimitKind.Stake, LimitPeriod.Day, 20_000, "ZAR", Now);

        effectiveAt.ShouldBe(Now.AddHours(24));
        limit.ShouldBe(Daily(10_000) with { PendingAmount = 20_000, PendingEffectiveAt = Now.AddHours(24) });
        limit!.AsOf(Now.AddHours(23))!.Amount.ShouldBe(10_000);
        limit.AsOf(Now.AddHours(24)).ShouldBe(Daily(20_000));
    }

    [Fact]
    public void Asking_again_for_the_same_raise_does_not_restart_the_clock()
    {
        var (first, due) = LimitPolicy.Change(Daily(10_000), LimitKind.Stake, LimitPeriod.Day, 20_000, "ZAR", Now);

        var (again, dueAgain) = LimitPolicy.Change(first, LimitKind.Stake, LimitPeriod.Day, 20_000, "ZAR", Now.AddHours(5));

        again.ShouldBe(first);
        dueAgain.ShouldBe(due);
    }

    [Fact]
    public void Removal_is_a_raise_and_the_limit_is_gone_once_it_is_due()
    {
        var (limit, effectiveAt) = LimitPolicy.Change(Daily(10_000), LimitKind.Stake, LimitPeriod.Day, null, "ZAR", Now);

        effectiveAt.ShouldBe(Now.AddHours(24));
        limit!.AsOf(Now.AddHours(1)).ShouldNotBeNull();
        limit.AsOf(Now.AddHours(24)).ShouldBeNull();
    }

    [Fact]
    public void Removing_a_limit_that_does_not_exist_changes_nothing() =>
        LimitPolicy.Change(null, LimitKind.Loss, LimitPeriod.Month, null, "ZAR", Now).Limit.ShouldBeNull();

    [Fact]
    public void A_due_raise_is_settled_before_the_next_change_is_judged()
    {
        var dueRaise = Daily(10_000) with { PendingAmount = 30_000, PendingEffectiveAt = Now.AddHours(-1) };

        var (limit, effectiveAt) = LimitPolicy.Change(dueRaise, LimitKind.Stake, LimitPeriod.Day, 25_000, "ZAR", Now);

        limit.ShouldBe(Daily(25_000));
        effectiveAt.ShouldBe(Now);
    }

    [Theory]
    [InlineData(0L, "ZAR", "invalid_limit")]
    [InlineData(-5L, "ZAR", "invalid_limit")]
    [InlineData(100L, "EUR", "unsupported_currency")]
    public void Invalid_requests_are_refused(long amount, string currency, string code) =>
        LimitPolicy.Validate(amount, currency)!.Code.ShouldBe(code);

    [Fact]
    public void Valid_request_and_removal_pass_validation()
    {
        LimitPolicy.Validate(100, "USD").ShouldBeNull();
        LimitPolicy.Validate(null, "ZAR").ShouldBeNull();
    }
}
