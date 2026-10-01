namespace SwiftBets.Compliance.Domain.Audit;

/// <summary>One audited action from any service. OccurredAt is kept to the millisecond in UTC, as stored.</summary>
public sealed record AuditRecord(
    Guid AuditId,
    string Service,
    string Actor,
    string Action,
    string SubjectType,
    string SubjectId,
    string? Before,
    string? After,
    string CorrelationId,
    DateTimeOffset OccurredAt)
{
    public AuditRecord Normalised() => this with
    {
        OccurredAt = new DateTimeOffset(OccurredAt.UtcTicks - (OccurredAt.UtcTicks % TimeSpan.TicksPerMillisecond), TimeSpan.Zero),
    };
}

/// <summary>A record at its place in the chain: Hash covers the previous hash and every field of the record.</summary>
public sealed record ChainedAuditRecord(long Sequence, AuditRecord Record, byte[] PreviousHash, byte[] Hash);
