namespace SwiftBets.Compliance.Domain;

/// <summary>
/// South African rules (D99): a lower limit applies at once; a higher limit, or removing one, applies only after the
/// cooling period, so a customer cannot lift a limit in the heat of the moment.
/// </summary>
public static class LimitPolicy
{
    public static readonly TimeSpan CoolingPeriod = TimeSpan.FromHours(24);

    /// <summary>The limit after the request (null: no limit), and when the requested value takes effect.</summary>
    public static (MoneyLimit? Limit, DateTimeOffset EffectiveAt) Change(
        MoneyLimit? existing, LimitKind kind, LimitPeriod period, long? amount, string currency, DateTimeOffset now)
    {
        var current = existing?.AsOf(now);
        if (current is null)
        {
            return amount is { } first ? (new MoneyLimit(kind, period, first, currency, null, null), now) : (null, now);
        }

        if (amount is { } lower && lower <= current.Amount)
        {
            return (current with { Amount = lower, PendingAmount = null, PendingEffectiveAt = null }, now);
        }

        // Asking again for the same raise keeps the original effective time instead of restarting the clock.
        if (current.HasPending && current.PendingAmount == amount)
        {
            return (current, current.PendingEffectiveAt!.Value);
        }

        var effectiveAt = now + CoolingPeriod;
        return (current with { PendingAmount = amount, PendingEffectiveAt = effectiveAt }, effectiveAt);
    }

    public static ComplianceError? Validate(long? amount, string currency)
    {
        if (amount is <= 0)
        {
            return new ComplianceError("invalid_limit", "A limit must be more than zero; remove it instead.");
        }

        return currency is "ZAR" or "USD" ? null : new ComplianceError("unsupported_currency", "Limits are set in ZAR or USD.");
    }
}
