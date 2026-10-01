using SwiftBets.BuildingBlocks.Core;
using SwiftBets.Compliance.Application;
using SwiftBets.Compliance.Domain;
using SwiftBets.Contracts.Compliance;
using SwiftBets.Contracts.Messaging;
using ContractKyc = SwiftBets.Contracts.Compliance.KycStatus;
using ContractLimit = SwiftBets.Contracts.Compliance.MoneyLimit;
using ContractLimitKind = SwiftBets.Contracts.Compliance.LimitKind;
using ContractPeriod = SwiftBets.Contracts.Compliance.LimitPeriod;
using ContractRestriction = SwiftBets.Contracts.Compliance.Restriction;
using ContractRestrictionKind = SwiftBets.Contracts.Compliance.RestrictionKind;
using Money = SwiftBets.Contracts.Money.Money;

namespace SwiftBets.Compliance.Infrastructure.Messaging;

/// <summary>Maps compliance state and events to the published contracts; every event is keyed by user id.</summary>
internal static class ComplianceContracts
{
    public static EventEnvelope<RestrictionsChangedV1> Snapshot(ComplianceState state, DateTimeOffset now) =>
        Envelope(new RestrictionsChangedV1(
            state.UserId,
            state.Revision,
            [.. state.Limits.Select(l => new ContractLimit(
                Map<ContractLimitKind>(l.Kind), Map<ContractPeriod>(l.Period), new Money(l.Amount, l.Currency),
                l.PendingAmount is { } pending ? new Money(pending, l.Currency) : null, l.PendingEffectiveAt))],
            [.. state.Restrictions.Select(r => new ContractRestriction(Map<ContractRestrictionKind>(r.Kind), r.StartsAt, r.EndsAt, r.Reason))],
            state.SessionLimitMinutes,
            state.RealityCheckMinutes,
            Map<ContractKyc>(state.KycStatus),
            now), now);

    public static EventEnvelope<LimitChangedV1> ToLimitChanged(Guid userId, LimitChanged changed, string actor, DateTimeOffset now) =>
        Envelope(new LimitChangedV1(
            userId, Map<ContractLimitKind>(changed.Kind), Map<ContractPeriod>(changed.Period),
            changed.PreviousAmount is { } previous ? new Money(previous, changed.Currency) : null,
            changed.Amount is { } amount ? new Money(amount, changed.Currency) : null,
            changed.EffectiveAt, actor, now), now);

    public static EventEnvelope<SelfExclusionStartedV1> ToExclusionStarted(Guid userId, ExclusionStarted started, string actor, DateTimeOffset now) =>
        Envelope(new SelfExclusionStartedV1(
            userId, Map<ContractRestrictionKind>(started.Restriction.Kind), started.Restriction.StartsAt, started.Restriction.EndsAt, started.Restriction.Reason, actor), now);

    public static EventEnvelope<KycStatusChangedV1> ToKycChanged(KycChanged changed, DateTimeOffset now) =>
        Envelope(new KycStatusChangedV1(
            changed.Case.UserId, changed.Case.CaseId, Map<ContractKyc>(changed.Previous), Map<ContractKyc>(changed.Case.Status), changed.Case.Provider, changed.Case.Reason, now), now);

    private static TOut Map<TOut>(Enum value)
        where TOut : struct, Enum => Enum.Parse<TOut>(value.ToString());

    private static EventEnvelope<T> Envelope<T>(T payload, DateTimeOffset now)
        where T : IEventContract =>
        EventEnvelope<T>.Create(payload, now, CorrelationContext.CorrelationId ?? CorrelationContext.NewId());
}
