using SwiftBets.Compliance.Application.Ports;
using SwiftBets.Compliance.Domain;
using SwiftBets.Contracts.Errors;
using SwiftBets.Contracts.Results;

namespace SwiftBets.Compliance.Application;

/// <summary>
/// Opens a verification case (Pending), asks the provider, then records its decision. Each step commits with
/// KycStatusChangedV1, a new snapshot and an audit entry, so withdrawals (E3) can gate on the status.
/// </summary>
public sealed class KycHandler(IComplianceStore store, IKycProvider provider, IKycCaseReader cases, TimeProvider time)
{
    public async Task<Result<ComplianceState>> SubmitAsync(Guid userId, KycDocument document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (KycPolicy.Validate(document) is { } invalid)
        {
            return Error.Validation(invalid.Code, invalid.Message);
        }

        var opened = await store.ChangeAsync(userId, "self", time.GetUtcNow(), state =>
        {
            if (KycPolicy.CanSubmit(state.KycStatus) is { } refused)
            {
                return Error.BusinessRule(refused.Code, refused.Message);
            }

            var now = time.GetUtcNow();
            var kycCase = new KycCase(Guid.CreateVersion7(now), userId, provider.Name, document.Type, document.Hint, KycStatus.Pending, null, now, null);
            return Result.Success(new ComplianceChange(state with { KycStatus = KycStatus.Pending }, "kyc.submitted", [new KycChanged(kycCase, state.KycStatus)]));
        }, cancellationToken);
        if (opened.IsFailure)
        {
            return opened;
        }

        var decision = await provider.VerifyAsync(userId, document, cancellationToken);
        var pending = (await cases.CasesAsync(userId, cancellationToken)).First(c => c.Status == KycStatus.Pending);
        return await store.ChangeAsync(userId, provider.Name, time.GetUtcNow(), state =>
        {
            var status = decision.Verified ? KycStatus.Verified : KycStatus.Rejected;
            var decided = pending with { Status = status, Reason = decision.Reason, DecidedAt = time.GetUtcNow() };
            return Result.Success(new ComplianceChange(state with { KycStatus = status }, decision.Verified ? "kyc.verified" : "kyc.rejected", [new KycChanged(decided, KycStatus.Pending)]));
        }, cancellationToken);
    }

    public Task<IReadOnlyList<KycCase>> CasesAsync(Guid userId, CancellationToken cancellationToken) => cases.CasesAsync(userId, cancellationToken);
}
