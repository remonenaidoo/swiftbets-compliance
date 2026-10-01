using SwiftBets.Compliance.Domain.Audit;

namespace SwiftBets.Compliance.Application.Audit;

public interface IAuditTrail
{
    /// <summary>Chains the record onto the end of the trail; false when that audit id is already there.</summary>
    Task<bool> AppendAsync(AuditRecord record, CancellationToken cancellationToken);

    /// <summary>Newest first; a null subject id lists every subject of the type, and null type lists everything.</summary>
    Task<IReadOnlyList<ChainedAuditRecord>> QueryAsync(string? subjectType, string? subjectId, int limit, CancellationToken cancellationToken);

    /// <summary>Entries after <paramref name="afterSequence"/> in chain order, at most <paramref name="batch"/>.</summary>
    Task<IReadOnlyList<ChainedAuditRecord>> ReadAsync(long afterSequence, int batch, CancellationToken cancellationToken);

    /// <summary>The last sequence and hash the trail recorded, kept apart from the entries so a cut tail is noticed.</summary>
    Task<(long Sequence, byte[] Hash)> HeadAsync(CancellationToken cancellationToken);
}
