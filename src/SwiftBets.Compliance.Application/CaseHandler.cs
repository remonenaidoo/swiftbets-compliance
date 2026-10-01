using SwiftBets.Compliance.Application.Ports;
using SwiftBets.Compliance.Domain;
using SwiftBets.Contracts.Errors;
using SwiftBets.Contracts.Results;

namespace SwiftBets.Compliance.Application;

/// <summary>Operator work on a customer: restrictions, four-eyes lifts and notes. Operator is the staff user id.</summary>
public sealed class CaseHandler(IComplianceStore store, ICaseStore cases, TimeProvider time)
{
    public Task<Result<ComplianceState>> AddRestrictionAsync(Guid userId, RestrictionKind kind, string reason, string operatorId, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        return store.ChangeAsync(userId, operatorId, now, state =>
        {
            var (restriction, error) = OperatorRestrictions.Add(kind, reason, state.Restrictions, now);
            return error is not null
                ? Error.BusinessRule(error.Code, error.Message)
                : Result.Success(new ComplianceChange(state with { Restrictions = [.. state.Restrictions, restriction!] }, "restriction.added", []));
        }, cancellationToken);
    }

    public async Task<Result<LiftRequest>> RequestLiftAsync(Guid userId, Guid restrictionId, string reason, string operatorId, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var state = await store.GetAsync(userId, now, cancellationToken);
        if (OperatorRestrictions.CanRequestLift(state.Restrictions.FirstOrDefault(r => r.RestrictionId == restrictionId), reason, now) is { } refused)
        {
            return refused.Code == "restriction_not_found" ? Error.NotFound(refused.Code, refused.Message) : Error.BusinessRule(refused.Code, refused.Message);
        }

        var request = new LiftRequest(Guid.CreateVersion7(now), userId, restrictionId, operatorId, reason.Trim(), now);
        return await cases.AddLiftRequestAsync(request, cancellationToken)
            ? Result.Success(request)
            : Error.Conflict("lift_already_requested", "A lift for this restriction is already waiting for approval.");
    }

    public async Task<Result<ComplianceState>> ApproveLiftAsync(Guid userId, Guid requestId, string approverId, CancellationToken cancellationToken)
    {
        if (await cases.PendingLiftRequestAsync(userId, requestId, cancellationToken) is not { } request)
        {
            return Error.NotFound("lift_request_not_found", "No such pending lift request.");
        }

        if (OperatorRestrictions.CanApprove(request, approverId) is { } refused)
        {
            return Error.BusinessRule(refused.Code, refused.Message);
        }

        var now = time.GetUtcNow();
        return await store.ChangeAsync(userId, approverId, now, state =>
        {
            var restriction = state.Restrictions.FirstOrDefault(r => r.RestrictionId == request.RestrictionId && r.IsActive(now));
            if (restriction is null)
            {
                return Error.NotFound("restriction_not_found", "The restriction has already ended.");
            }

            var next = state with { Restrictions = [.. state.Restrictions.Select(r => r.RestrictionId == restriction.RestrictionId ? r with { EndsAt = now } : r)] };
            return Result.Success(new ComplianceChange(next, "restriction.lifted", [new RestrictionLifted(restriction.RestrictionId, request.RequestId)]));
        }, cancellationToken);
    }

    public Task<IReadOnlyList<LiftRequest>> PendingLiftsAsync(Guid userId, CancellationToken cancellationToken) => cases.PendingLiftRequestsAsync(userId, cancellationToken);

    public async Task<Result<Note>> AddNoteAsync(Guid userId, string body, string operatorId, CancellationToken cancellationToken) =>
        string.IsNullOrWhiteSpace(body) || body.Length > 4000
            ? Error.Validation("invalid_note", "A note is 1 to 4000 characters.")
            : Result.Success(await cases.AddNoteAsync(userId, operatorId, body.Trim(), time.GetUtcNow(), cancellationToken));

    public Task<IReadOnlyList<Note>> NotesAsync(Guid userId, CancellationToken cancellationToken) => cases.NotesAsync(userId, cancellationToken);
}
