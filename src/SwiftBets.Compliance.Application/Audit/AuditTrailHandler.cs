using SwiftBets.Compliance.Domain.Audit;

namespace SwiftBets.Compliance.Application.Audit;

/// <summary>Outcome of walking the whole chain: Entries checked, and the first sequence whose hash does not hold.</summary>
public sealed record ChainVerification(bool Verified, long Entries, long? BrokenAt);

public sealed class AuditTrailHandler(IAuditTrail trail)
{
    private const int Batch = 1_000;

    public Task<bool> AppendAsync(AuditRecord record, CancellationToken cancellationToken) => trail.AppendAsync(record, cancellationToken);

    public Task<IReadOnlyList<ChainedAuditRecord>> QueryAsync(string? subjectType, string? subjectId, int limit, CancellationToken cancellationToken) =>
        trail.QueryAsync(subjectType, subjectId, Math.Clamp(limit, 1, 500), cancellationToken);

    /// <summary>Recomputes every hash from the start, in batches, so the check never trusts a stored hash it has not derived.</summary>
    public async Task<ChainVerification> VerifyAsync(CancellationToken cancellationToken)
    {
        var (headSequence, headHash) = await trail.HeadAsync(cancellationToken);
        var previous = AuditChain.Genesis;
        long after = 0, count = 0;
        while (true)
        {
            var batch = await trail.ReadAsync(after, Batch, cancellationToken);
            if (batch.Count == 0)
            {
                var intact = after == headSequence && previous.AsSpan().SequenceEqual(headHash);
                return new ChainVerification(intact, count, intact ? null : headSequence);
            }

            if (AuditChain.FirstBreak(previous, batch, out previous) is { } broken)
            {
                return new ChainVerification(false, count + batch.TakeWhile(e => e.Sequence != broken).Count(), broken);
            }

            count += batch.Count;
            after = batch[^1].Sequence;
        }
    }
}
