namespace SwiftBets.Compliance.Domain;

/// <summary>Everything responsible gambling holds about one account. Revision rises on every committed change.</summary>
public sealed record ComplianceState(
    Guid UserId,
    long Revision,
    IReadOnlyList<MoneyLimit> Limits,
    IReadOnlyList<Restriction> Restrictions,
    int? SessionLimitMinutes,
    int? RealityCheckMinutes,
    KycStatus KycStatus)
{
    public static ComplianceState New(Guid userId) => new(userId, 0, [], [], null, null, KycStatus.NotStarted);

    public MoneyLimit? Limit(LimitKind kind, LimitPeriod period) => Limits.FirstOrDefault(l => l.Kind == kind && l.Period == period);

    public bool IsExcluded(DateTimeOffset now) => Restrictions.Any(r => r.IsExclusion && r.IsActive(now));

    public ComplianceState WithLimit(LimitKind kind, LimitPeriod period, MoneyLimit? limit) =>
        this with { Limits = [.. Limits.Where(l => l.Kind != kind || l.Period != period), .. limit is null ? [] : new[] { limit }] };

    /// <summary>Applies pending changes that are due and drops restrictions that have ended, so snapshots stay small.</summary>
    public ComplianceState Settled(DateTimeOffset now) =>
        this with
        {
            Limits = [.. Limits.Select(l => l.AsOf(now)).OfType<MoneyLimit>()],
            Restrictions = [.. Restrictions.Where(r => r.EndsAt is null || r.EndsAt > now)],
        };
}
