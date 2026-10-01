using Dapper;
using SwiftBets.BuildingBlocks.Persistence;
using SwiftBets.Compliance.Application.Ports;
using SwiftBets.Compliance.Domain;

namespace SwiftBets.Compliance.Infrastructure.Kyc;

public sealed class SqlKycCaseReader(ISqlConnectionFactory connections) : IKycCaseReader
{
    private static readonly SqlResources Sql = SqlResources.For<SqlKycCaseReader>();

    public async Task<IReadOnlyList<KycCase>> CasesAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<Row>(new CommandDefinition(Sql.Get("Kyc.Cases"), new { UserId = userId }, cancellationToken: cancellationToken));
        return [.. rows.Select(r => new KycCase(r.CaseId, r.UserId, r.Provider, (KycDocumentType)r.DocumentType, r.DocumentHint, (KycStatus)r.Status, r.Reason, r.CreatedAt, r.DecidedAt))];
    }

    private sealed record Row(Guid CaseId, Guid UserId, string Provider, byte DocumentType, string DocumentHint, byte Status, string? Reason, DateTimeOffset CreatedAt, DateTimeOffset? DecidedAt);
}
