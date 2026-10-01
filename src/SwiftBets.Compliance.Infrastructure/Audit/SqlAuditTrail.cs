using Dapper;
using SwiftBets.BuildingBlocks.Persistence;
using SwiftBets.Compliance.Application.Audit;
using SwiftBets.Compliance.Domain.Audit;

namespace SwiftBets.Compliance.Infrastructure.Audit;

/// <summary>The hash-chained trail in SQL Server; appends serialise on the head row.</summary>
public sealed class SqlAuditTrail(ISqlConnectionFactory connections, TimeProvider time) : IAuditTrail
{
    private static readonly SqlResources Sql = SqlResources.For<SqlAuditTrail>();

    public async Task<bool> AppendAsync(AuditRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        var r = record.Normalised();
        await using var connection = await connections.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var head = await connection.QuerySingleAsync<HeadRow>(new CommandDefinition(Sql.Get("Audit.LockHead"), transaction: transaction, cancellationToken: cancellationToken));
        // Checked under the head lock, so a redelivered event racing its first delivery is still stored once.
        if (await connection.ExecuteScalarAsync<int>(new CommandDefinition(Sql.Get("Audit.Exists"), new { r.AuditId }, transaction, cancellationToken: cancellationToken)) > 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        var hash = AuditChain.HashOf(head.Hash, r);
        await connection.ExecuteAsync(new CommandDefinition(Sql.Get("Audit.Insert"), new
        {
            Sequence = head.Sequence + 1, r.AuditId, r.Service, r.Actor, r.Action, r.SubjectType, r.SubjectId, r.Before, r.After, r.CorrelationId,
            r.OccurredAt, RecordedAt = time.GetUtcNow(), PreviousHash = head.Hash, Hash = hash,
        }, transaction, cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<ChainedAuditRecord>> QueryAsync(string? subjectType, string? subjectId, int limit, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<EntryRow>(new CommandDefinition(Sql.Get("Audit.Query"), new { SubjectType = subjectType, SubjectId = subjectId, Limit = limit }, cancellationToken: cancellationToken));
        return [.. rows.Select(r => r.ToDomain())];
    }

    public async Task<IReadOnlyList<ChainedAuditRecord>> ReadAsync(long afterSequence, int batch, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<EntryRow>(new CommandDefinition(Sql.Get("Audit.Read"), new { After = afterSequence, Batch = batch }, cancellationToken: cancellationToken));
        return [.. rows.Select(r => r.ToDomain())];
    }

    public async Task<(long Sequence, byte[] Hash)> HeadAsync(CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        var head = await connection.QuerySingleAsync<HeadRow>(new CommandDefinition(Sql.Get("Audit.Head"), cancellationToken: cancellationToken));
        return (head.Sequence, head.Hash);
    }

    private sealed record HeadRow(long Sequence, byte[] Hash);

    private sealed record EntryRow(
        long Sequence, Guid AuditId, string Service, string Actor, string Action, string SubjectType, string SubjectId,
        string? Before, string? After, string CorrelationId, DateTimeOffset OccurredAt, byte[] PreviousHash, byte[] Hash)
    {
        public ChainedAuditRecord ToDomain() =>
            new(Sequence, new AuditRecord(AuditId, Service, Actor, Action, SubjectType, SubjectId, Before, After, CorrelationId, OccurredAt), PreviousHash, Hash);
    }
}
