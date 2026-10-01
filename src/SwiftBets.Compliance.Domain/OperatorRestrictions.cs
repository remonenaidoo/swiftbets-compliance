namespace SwiftBets.Compliance.Domain;

/// <summary>A request by one operator to lift a block early; a second, different operator must approve it.</summary>
public sealed record LiftRequest(Guid RequestId, Guid UserId, Guid RestrictionId, string RequestedBy, string Reason, DateTimeOffset RequestedAt);

/// <summary>
/// Blocks operators place on part of an account (deposits, betting, withdrawals, marketing). Lifting one early needs
/// four eyes: whoever asks cannot approve. Cooling-off and self-exclusion are never lifted early (D99).
/// </summary>
public static class OperatorRestrictions
{
    public static readonly IReadOnlySet<RestrictionKind> Kinds =
        new HashSet<RestrictionKind> { RestrictionKind.NoDeposits, RestrictionKind.NoBetting, RestrictionKind.NoWithdrawals, RestrictionKind.NoMarketing };

    public static (Restriction? Restriction, ComplianceError? Error) Add(RestrictionKind kind, string reason, IReadOnlyCollection<Restriction> existing, DateTimeOffset now)
    {
        if (!Kinds.Contains(kind))
        {
            return (null, new ComplianceError("not_an_operator_restriction", "Operators block deposits, betting, withdrawals or marketing; exclusions come from the customer."));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return (null, new ComplianceError("reason_required", "Give a reason for the restriction."));
        }

        return existing.Any(r => r.Kind == kind && r.IsActive(now))
            ? (null, new ComplianceError("already_restricted", "The account already has that restriction."))
            : (new Restriction(Guid.CreateVersion7(now), kind, now, null, reason.Trim()), null);
    }

    public static ComplianceError? CanRequestLift(Restriction? restriction, string reason, DateTimeOffset now) =>
        restriction is null || !restriction.IsActive(now) ? new ComplianceError("restriction_not_found", "No such active restriction.")
        : restriction.IsExclusion ? new ComplianceError("exclusion_not_liftable", "A cooling-off or self-exclusion runs its full period.")
        : string.IsNullOrWhiteSpace(reason) ? new ComplianceError("reason_required", "Give a reason for lifting it.")
        : null;

    public static ComplianceError? CanApprove(LiftRequest request, string approver) =>
        string.Equals(request.RequestedBy, approver, StringComparison.OrdinalIgnoreCase)
            ? new ComplianceError("four_eyes_required", "Another operator must approve a lift you asked for.")
            : null;
}
