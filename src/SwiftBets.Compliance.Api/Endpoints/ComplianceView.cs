using SwiftBets.Compliance.Domain;

namespace SwiftBets.Compliance.Api.Endpoints;

/// <summary>What the customer and the console see: limits with any pending change, active blocks and session settings.</summary>
public sealed record ComplianceView(
    IReadOnlyList<ComplianceView.LimitView> Limits,
    IReadOnlyList<ComplianceView.RestrictionView> Restrictions,
    int? SessionLimitMinutes,
    int? RealityCheckMinutes,
    KycStatus KycStatus,
    bool Excluded)
{
    public static ComplianceView From(ComplianceState state, DateTimeOffset now) => new(
        [.. state.Limits.OrderBy(l => l.Kind).ThenBy(l => l.Period).Select(l => new LimitView(l.Kind, l.Period, l.Amount, l.Currency, l.PendingAmount, l.PendingEffectiveAt, l.HasPending && l.PendingAmount is null))],
        [.. state.Restrictions.Where(r => r.IsActive(now)).Select(r => new RestrictionView(r.RestrictionId, r.Kind, r.StartsAt, r.EndsAt, r.Reason))],
        state.SessionLimitMinutes,
        state.RealityCheckMinutes,
        state.KycStatus,
        state.IsExcluded(now));

    /// <summary>Amounts in minor units. PendingRemoval is true when the limit ends at PendingEffectiveAt.</summary>
    public sealed record LimitView(LimitKind Kind, LimitPeriod Period, long Amount, string Currency, long? PendingAmount, DateTimeOffset? PendingEffectiveAt, bool PendingRemoval);

    public sealed record RestrictionView(Guid Id, RestrictionKind Kind, DateTimeOffset StartsAt, DateTimeOffset? EndsAt, string Reason);
}
