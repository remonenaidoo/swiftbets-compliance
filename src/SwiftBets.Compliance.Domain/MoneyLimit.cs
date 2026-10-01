namespace SwiftBets.Compliance.Domain;

/// <summary>
/// A limit in minor units. Amount applies now; once PendingEffectiveAt has passed, PendingAmount applies instead, and a
/// null PendingAmount means the limit is gone.
/// </summary>
public sealed record MoneyLimit(LimitKind Kind, LimitPeriod Period, long Amount, string Currency, long? PendingAmount, DateTimeOffset? PendingEffectiveAt)
{
    public bool HasPending => PendingEffectiveAt is not null;

    /// <summary>The limit as it stands at <paramref name="now"/>: a due pending change is applied, or null when it removed the limit.</summary>
    public MoneyLimit? AsOf(DateTimeOffset now) =>
        PendingEffectiveAt is { } due && due <= now
            ? PendingAmount is { } raised ? this with { Amount = raised, PendingAmount = null, PendingEffectiveAt = null } : null
            : this;
}
