namespace SwiftBets.Compliance.Domain;

/// <summary>A block on part or all of an account; a null EndsAt lasts until an operator lifts it.</summary>
public sealed record Restriction(Guid RestrictionId, RestrictionKind Kind, DateTimeOffset StartsAt, DateTimeOffset? EndsAt, string Reason)
{
    public bool IsActive(DateTimeOffset now) => StartsAt <= now && (EndsAt is null || EndsAt > now);

    /// <summary>Cooling-off and self-exclusion block the whole account, marketing included.</summary>
    public bool IsExclusion => Kind is RestrictionKind.CoolingOff or RestrictionKind.SelfExclusion;
}
