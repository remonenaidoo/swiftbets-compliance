namespace SwiftBets.Compliance.Domain;

/// <summary>
/// Cooling-off is a short break of one day to six weeks. Self-exclusion lasts at least six months (D99) and up to five
/// years; neither can be shortened or lifted early by the customer.
/// </summary>
public static class ExclusionPolicy
{
    public static readonly TimeSpan MinimumCoolingOff = TimeSpan.FromDays(1);
    public static readonly TimeSpan MaximumCoolingOff = TimeSpan.FromDays(42);
    public const int MinimumSelfExclusionMonths = 6;
    public const int MaximumSelfExclusionMonths = 60;

    public static (Restriction? Restriction, ComplianceError? Error) Start(
        RestrictionKind kind, int? days, int? months, string reason, IReadOnlyCollection<Restriction> existing, DateTimeOffset now)
    {
        DateTimeOffset endsAt;
        switch (kind)
        {
            case RestrictionKind.CoolingOff when days is { } d && TimeSpan.FromDays(d) >= MinimumCoolingOff && TimeSpan.FromDays(d) <= MaximumCoolingOff:
                endsAt = now.AddDays(d);
                break;
            case RestrictionKind.CoolingOff:
                return (null, new ComplianceError("invalid_cooling_off", "A cooling-off lasts from 1 to 42 days."));
            case RestrictionKind.SelfExclusion when months is >= MinimumSelfExclusionMonths and <= MaximumSelfExclusionMonths:
                endsAt = now.AddMonths(months.Value);
                break;
            case RestrictionKind.SelfExclusion:
                return (null, new ComplianceError("invalid_self_exclusion", "A self-exclusion lasts from 6 to 60 months."));
            default:
                return (null, new ComplianceError("not_an_exclusion", "Only cooling-off and self-exclusion can be started by the customer."));
        }

        // A longer block may replace a shorter one; nothing may shorten an active block.
        if (existing.Where(r => r.IsExclusion && r.IsActive(now)).Any(r => r.EndsAt is null || r.EndsAt >= endsAt))
        {
            return (null, new ComplianceError("already_excluded", "The account is already excluded for at least that long."));
        }

        return (new Restriction(Guid.CreateVersion7(now), kind, now, endsAt, string.IsNullOrWhiteSpace(reason) ? "customer request" : reason.Trim()), null);
    }
}
