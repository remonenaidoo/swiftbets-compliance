using SwiftBets.Compliance.Domain;
using SwiftBets.Contracts.Results;

namespace SwiftBets.Compliance.Application.Ports;

public interface IComplianceStore
{
    /// <summary>The account's state, settled at <paramref name="now"/>; an unknown account has the empty state.</summary>
    Task<ComplianceState> GetAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>
    /// Locks the account, settles its state, applies <paramref name="change"/> and, when it succeeds, stores the next state
    /// with its events, snapshot and audit entry in one transaction. Concurrent changes to one account run one at a time.
    /// </summary>
    Task<Result<ComplianceState>> ChangeAsync(
        Guid userId, string actor, DateTimeOffset now, Func<ComplianceState, Result<ComplianceChange>> change, CancellationToken cancellationToken);
}
