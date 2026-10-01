using Dapper;
using Microsoft.Data.SqlClient;
using SwiftBets.BuildingBlocks.Outbox;
using SwiftBets.BuildingBlocks.Persistence;
using SwiftBets.Compliance.Application;
using SwiftBets.Compliance.Application.Ports;
using SwiftBets.Compliance.Domain;
using SwiftBets.Compliance.Infrastructure.Messaging;
using SwiftBets.Contracts.Messaging;
using SwiftBets.Contracts.Results;

namespace SwiftBets.Compliance.Infrastructure.Persistence;

/// <summary>Responsible-gambling state in SQL Server. Every change commits with its events, its snapshot and its audit entry.</summary>
public sealed class SqlComplianceStore(ISqlConnectionFactory connections, IOutbox outbox, IAuditWriter audit) : IComplianceStore
{
    private static readonly SqlResources Sql = SqlResources.For<SqlComplianceStore>();

    public async Task<ComplianceState> GetAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        return await LoadAsync(connection, null, userId, now, cancellationToken);
    }

    public async Task<Result<ComplianceState>> ChangeAsync(
        Guid userId, string actor, DateTimeOffset now, Func<ComplianceState, Result<ComplianceChange>> change, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(Sql.Get("Accounts.Lock"), new { UserId = userId, Now = now }, transaction, cancellationToken: cancellationToken));
        var state = await LoadAsync(connection, transaction, userId, now, cancellationToken);

        var outcome = change(state);
        if (outcome.IsFailure || outcome.Value.IsUnchanged)
        {
            await transaction.RollbackAsync(cancellationToken);
            return outcome.IsFailure ? Result.Failure<ComplianceState>(outcome.Error!) : Result.Success(state);
        }

        var next = outcome.Value.Next with { Revision = state.Revision + 1 };
        await SaveAsync(connection, transaction, state, next, actor, now, cancellationToken);

        var key = userId.ToString();
        foreach (var @event in outcome.Value.Events)
        {
            await PublishAsync(transaction, userId, actor, @event, now, cancellationToken);
        }

        await outbox.EnqueueAsync(transaction, Topics.RestrictionsChanged, key, ComplianceContracts.Snapshot(next, now), cancellationToken);
        await audit.RecordAsync(transaction, new AuditEntry(actor, outcome.Value.Action, "user", key, state, next), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success(next);
    }

    public async Task<IReadOnlyList<Guid>> EndedExclusionsAsync(DateTimeOffset now, int batch, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        return [.. await connection.QueryAsync<Guid>(new CommandDefinition(Sql.Get("Restrictions.EndedUnannounced"), new { Now = now, Batch = batch }, cancellationToken: cancellationToken))];
    }

    private Task PublishAsync(SqlTransaction transaction, Guid userId, string actor, ComplianceEvent @event, DateTimeOffset now, CancellationToken cancellationToken) =>
        @event switch
        {
            LimitChanged limit => outbox.EnqueueAsync(transaction, Topics.LimitChanged, userId.ToString(), ComplianceContracts.ToLimitChanged(userId, limit, actor, now), cancellationToken),
            RestrictionLifted lifted => transaction.Connection!.ExecuteAsync(new CommandDefinition(Sql.Get("Restrictions.Lift"),
                new { lifted.RestrictionId, lifted.RequestId, By = actor, Now = now }, transaction, cancellationToken: cancellationToken)),
            ExclusionsEnded => transaction.Connection!.ExecuteAsync(new CommandDefinition(Sql.Get("Restrictions.MarkEndAnnounced"), new { UserId = userId, Now = now }, transaction, cancellationToken: cancellationToken)),
            ExclusionStarted exclusion => outbox.EnqueueAsync(transaction, Topics.SelfExclusionStarted, userId.ToString(), ComplianceContracts.ToExclusionStarted(userId, exclusion, actor, now), cancellationToken),
            _ => throw new InvalidOperationException($"No contract for {@event.GetType().Name}."),
        };

    private static async Task SaveAsync(SqlConnection connection, SqlTransaction transaction, ComplianceState before, ComplianceState next, string actor, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(Sql.Get("Accounts.Update"), new
        {
            next.UserId, next.Revision, next.SessionLimitMinutes, next.RealityCheckMinutes, KycStatus = (byte)next.KycStatus, Now = now,
        }, transaction, cancellationToken: cancellationToken));

        // Limits are few per account, so they are rewritten whole; this also stores any pending change that fell due.
        await connection.ExecuteAsync(new CommandDefinition(Sql.Get("Limits.DeleteForUser"), new { next.UserId }, transaction, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition(Sql.Get("Limits.Insert"), next.Limits.Select(l => new
        {
            next.UserId, Kind = (byte)l.Kind, Period = (byte)l.Period, l.Amount, l.Currency, l.PendingAmount, l.PendingEffectiveAt,
        }), transaction, cancellationToken: cancellationToken));

        var known = before.Restrictions.Select(r => r.RestrictionId).ToHashSet();
        await connection.ExecuteAsync(new CommandDefinition(Sql.Get("Restrictions.Insert"), next.Restrictions.Where(r => !known.Contains(r.RestrictionId)).Select(r => new
        {
            r.RestrictionId, next.UserId, Kind = (byte)r.Kind, r.StartsAt, r.EndsAt, r.Reason, CreatedBy = actor,
        }), transaction, cancellationToken: cancellationToken));
    }

    private static async Task<ComplianceState> LoadAsync(SqlConnection connection, SqlTransaction? transaction, Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var grid = await connection.QueryMultipleAsync(new CommandDefinition(Sql.Get("Accounts.Load"), new { UserId = userId, Now = now }, transaction, cancellationToken: cancellationToken));
        var account = await grid.ReadSingleOrDefaultAsync<AccountRow>();
        var limits = (await grid.ReadAsync<LimitRow>()).Select(r => r.ToDomain()).ToList();
        var restrictions = (await grid.ReadAsync<RestrictionRow>()).Select(r => r.ToDomain()).ToList();
        if (account is null)
        {
            return ComplianceState.New(userId);
        }

        return new ComplianceState(userId, account.Revision, limits, restrictions, account.SessionLimitMinutes, account.RealityCheckMinutes, (KycStatus)account.KycStatus).Settled(now);
    }
}
