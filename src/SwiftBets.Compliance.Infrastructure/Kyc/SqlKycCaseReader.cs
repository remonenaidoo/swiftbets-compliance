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
        return [.. rows.Select(r => r.ToDomain())];
    }

    public async Task<KycCase?> CaseAsync(Guid caseId, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        return (await connection.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(Sql.Get("Kyc.Case"), new { CaseId = caseId }, cancellationToken: cancellationToken)))?.ToDomain();
    }

    public async Task<IReadOnlyList<KycCase>> AwaitingReviewAsync(int limit, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<Row>(new CommandDefinition(Sql.Get("Kyc.AwaitingReview"), new { Limit = limit, Provider = KycUploadPolicy.ReviewProvider }, cancellationToken: cancellationToken));
        return [.. rows.Select(r => r.ToDomain())];
    }

    public async Task<IReadOnlyList<KycFile>> FilesAsync(Guid caseId, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        return [.. (await connection.QueryAsync<FileRow>(new CommandDefinition(Sql.Get("Kyc.Files"), new { CaseId = caseId }, cancellationToken: cancellationToken))).Select(r => r.ToDomain())];
    }

    public async Task<KycFile?> FileAsync(Guid fileId, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        return (await connection.QuerySingleOrDefaultAsync<FileRow>(new CommandDefinition(Sql.Get("Kyc.File"), new { FileId = fileId }, cancellationToken: cancellationToken)))?.ToDomain();
    }

    private sealed record Row(Guid CaseId, Guid UserId, string Provider, byte DocumentType, string DocumentHint, byte Status, string? Reason, DateTimeOffset CreatedAt, DateTimeOffset? DecidedAt, string? LegalName)
    {
        public KycCase ToDomain() => new(CaseId, UserId, Provider, (KycDocumentType)DocumentType, DocumentHint, (KycStatus)Status, Reason, CreatedAt, DecidedAt, LegalName);
    }

    private sealed record FileRow(Guid FileId, Guid CaseId, Guid UserId, byte Kind, string ContentType, long SizeBytes, string StorageKey, DateTimeOffset UploadedAt)
    {
        public KycFile ToDomain() => new(FileId, CaseId, UserId, (KycFileKind)Kind, ContentType, SizeBytes, StorageKey, UploadedAt);
    }
}
