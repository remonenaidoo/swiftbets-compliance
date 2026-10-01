using SwiftBets.Compliance.Application.Ports;
using SwiftBets.Compliance.Domain;
using SwiftBets.Contracts.Errors;
using SwiftBets.Contracts.Results;

namespace SwiftBets.Compliance.Application;

/// <summary>Customer and operator changes to limits, exclusions and session settings. Actor is a user id or "self".</summary>
public sealed class ComplianceHandler(IComplianceStore store, TimeProvider time)
{
    public Task<ComplianceState> GetAsync(Guid userId, CancellationToken cancellationToken) =>
        store.GetAsync(userId, time.GetUtcNow(), cancellationToken);

    /// <summary>Sets, lowers, raises or (with a null amount) removes a limit; raises and removals wait out the cooling period.</summary>
    public Task<Result<ComplianceState>> SetLimitAsync(
        Guid userId, LimitKind kind, LimitPeriod period, long? amount, string currency, string actor, CancellationToken cancellationToken)
    {
        if (LimitPolicy.Validate(amount, currency) is { } invalid)
        {
            return Task.FromResult(Result.Failure<ComplianceState>(Error.Validation(invalid.Code, invalid.Message)));
        }

        var now = time.GetUtcNow();
        return store.ChangeAsync(userId, actor, now, state =>
        {
            var existing = state.Limit(kind, period);
            if (existing is not null && existing.Currency != currency)
            {
                return Error.BusinessRule("currency_mismatch", $"This limit is held in {existing.Currency}.");
            }

            var (limit, effectiveAt) = LimitPolicy.Change(existing, kind, period, amount, currency, now);
            if (limit == existing)
            {
                return Result.Success(ComplianceChange.Unchanged(state));
            }

            var changed = new LimitChanged(kind, period, existing?.Amount, amount, currency, effectiveAt);
            return Result.Success(new ComplianceChange(state.WithLimit(kind, period, limit), amount is null ? "limit.removed" : "limit.set", [changed]));
        }, cancellationToken);
    }

    public Task<Result<ComplianceState>> StartExclusionAsync(
        Guid userId, RestrictionKind kind, int? days, int? months, string reason, string actor, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        return store.ChangeAsync(userId, actor, now, state =>
        {
            var (restriction, error) = ExclusionPolicy.Start(kind, days, months, reason, state.Restrictions, now);
            if (error is not null)
            {
                return Error.BusinessRule(error.Code, error.Message);
            }

            var next = state with { Restrictions = [.. state.Restrictions, restriction!] };
            return Result.Success(new ComplianceChange(next, kind == RestrictionKind.SelfExclusion ? "self-exclusion.started" : "cooling-off.started", [new ExclusionStarted(restriction!)]));
        }, cancellationToken);
    }

    /// <summary>Announces exclusions that have run out, so identity reopens the account; returns how many accounts.</summary>
    public async Task<int> AnnounceEndedExclusionsAsync(CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var users = await store.EndedExclusionsAsync(now, 100, cancellationToken);
        foreach (var userId in users)
        {
            await store.ChangeAsync(userId, "compliance", now, state => Result.Success(new ComplianceChange(state, "exclusion.ended", [new ExclusionsEnded()])), cancellationToken);
        }

        return users.Count;
    }

    public Task<Result<ComplianceState>> SetSessionSettingsAsync(
        Guid userId, int? sessionLimitMinutes, int? realityCheckMinutes, string actor, CancellationToken cancellationToken)
    {
        if (SessionSettings.Validate(sessionLimitMinutes, realityCheckMinutes) is { } invalid)
        {
            return Task.FromResult(Result.Failure<ComplianceState>(Error.Validation(invalid.Code, invalid.Message)));
        }

        return store.ChangeAsync(userId, actor, time.GetUtcNow(), state =>
            Result.Success(new ComplianceChange(state with { SessionLimitMinutes = sessionLimitMinutes, RealityCheckMinutes = realityCheckMinutes }, "session-settings.set", [])),
            cancellationToken);
    }
}
