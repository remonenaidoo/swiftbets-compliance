using SwiftBets.Compliance.Domain;

namespace SwiftBets.Compliance.Infrastructure.Persistence;

internal sealed record AccountRow(Guid UserId, long Revision, int? SessionLimitMinutes, int? RealityCheckMinutes, byte KycStatus);

internal sealed record LimitRow(byte Kind, byte Period, long Amount, string Currency, long? PendingAmount, DateTimeOffset? PendingEffectiveAt)
{
    public MoneyLimit ToDomain() => new((LimitKind)Kind, (LimitPeriod)Period, Amount, Currency, PendingAmount, PendingEffectiveAt);
}

internal sealed record RestrictionRow(Guid RestrictionId, byte Kind, DateTimeOffset StartsAt, DateTimeOffset? EndsAt, string Reason)
{
    public Restriction ToDomain() => new(RestrictionId, (RestrictionKind)Kind, StartsAt, EndsAt, Reason);
}
