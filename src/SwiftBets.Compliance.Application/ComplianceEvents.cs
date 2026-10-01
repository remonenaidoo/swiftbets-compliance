using SwiftBets.Compliance.Domain;

namespace SwiftBets.Compliance.Application;

/// <summary>What a committed change announces besides the new snapshot.</summary>
public abstract record ComplianceEvent;

/// <summary>Null amounts mean no limit; EffectiveAt is when Amount applies.</summary>
public sealed record LimitChanged(LimitKind Kind, LimitPeriod Period, long? PreviousAmount, long? Amount, string Currency, DateTimeOffset EffectiveAt) : ComplianceEvent;

public sealed record ExclusionStarted(Restriction Restriction) : ComplianceEvent;

/// <summary>Cooling-off or self-exclusion periods that have run out; the new snapshot announces it.</summary>
public sealed record ExclusionsEnded : ComplianceEvent;

/// <summary>A rule's accepted outcome: the next state, the audit action name and the events to publish with it.</summary>
public sealed record ComplianceChange(ComplianceState Next, string Action, IReadOnlyList<ComplianceEvent> Events)
{
    /// <summary>Nothing to store: no new revision, no events and no audit entry.</summary>
    public static ComplianceChange Unchanged(ComplianceState state) => new(state, string.Empty, []);

    public bool IsUnchanged => Action.Length == 0;
}
