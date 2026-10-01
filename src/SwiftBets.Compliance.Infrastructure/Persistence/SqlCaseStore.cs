using Dapper;
using Microsoft.Data.SqlClient;
using SwiftBets.BuildingBlocks.Outbox;
using SwiftBets.BuildingBlocks.Persistence;
using SwiftBets.Compliance.Application.Ports;
using SwiftBets.Compliance.Domain;

namespace SwiftBets.Compliance.Infrastructure.Persistence;

/// <summary>Notes and lift requests; each write commits with its audit entry.</summary>
public sealed class SqlCaseStore(ISqlConnectionFactory connections, IAuditWriter audit) : ICaseStore
{
    private static readonly SqlResources Sql = SqlResources.For<SqlCaseStore>();

    public async Task<Note> AddNoteAsync(Guid userId, string author, string body, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var note = new Note(Guid.CreateVersion7(now), userId, author, body, now);
        await using var connection = await connections.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(Sql.Get("Notes.Insert"), note, transaction, cancellationToken: cancellationToken));
        await audit.RecordAsync(transaction, new AuditEntry(author, "note.added", "user", userId.ToString(), After: new { note.NoteId, note.Body }), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return note;
    }

    public async Task<IReadOnlyList<Note>> NotesAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        return [.. await connection.QueryAsync<Note>(new CommandDefinition(Sql.Get("Notes.List"), new { UserId = userId }, cancellationToken: cancellationToken))];
    }

    public async Task<bool> AddLiftRequestAsync(LiftRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var connection = await connections.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(Sql.Get("LiftRequests.Insert"), request, transaction, cancellationToken: cancellationToken));
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        await audit.RecordAsync(transaction, new AuditEntry(request.RequestedBy, "restriction.lift-requested", "user", request.UserId.ToString(),
            After: new { request.RequestId, request.RestrictionId, request.Reason }), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<LiftRequest?> PendingLiftRequestAsync(Guid userId, Guid requestId, CancellationToken cancellationToken) =>
        (await PendingAsync(userId, requestId, cancellationToken)).SingleOrDefault();

    public Task<IReadOnlyList<LiftRequest>> PendingLiftRequestsAsync(Guid userId, CancellationToken cancellationToken) =>
        PendingAsync(userId, null, cancellationToken);

    private async Task<IReadOnlyList<LiftRequest>> PendingAsync(Guid userId, Guid? requestId, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        return [.. await connection.QueryAsync<LiftRequest>(new CommandDefinition(Sql.Get("LiftRequests.Pending"), new { UserId = userId, RequestId = requestId }, cancellationToken: cancellationToken))];
    }
}
