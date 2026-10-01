using SwiftBets.Compliance.Application.Audit;
using SwiftBets.Compliance.Domain.Audit;
using SwiftBets.Contracts.Serialization;

namespace SwiftBets.Compliance.Api.Endpoints;

/// <summary>The audit view for staff: entries by subject, newest first, and a full check of the hash chain.</summary>
public static class AuditEndpoints
{
    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var audit = endpoints.MapGroup("/admin/audit").RequireAuthorization(CompliancePermissions.AuditRead);

        audit.MapGet("/", async (string? subjectType, string? subjectId, int? limit, AuditTrailHandler trail, CancellationToken cancellationToken) =>
            Results.Json((await trail.QueryAsync(subjectType, subjectId, limit ?? 100, cancellationToken)).Select(AuditEntryView.From), ContractJson.Options));

        audit.MapGet("/verify", async (AuditTrailHandler trail, CancellationToken cancellationToken) =>
            Results.Json(await trail.VerifyAsync(cancellationToken), ContractJson.Options));

        return endpoints;
    }

    public sealed record AuditEntryView(
        long Sequence, Guid AuditId, string Service, string Actor, string Action, string SubjectType, string SubjectId,
        string? Before, string? After, string CorrelationId, DateTimeOffset OccurredAt, string Hash)
    {
        public static AuditEntryView From(ChainedAuditRecord entry) => new(
            entry.Sequence, entry.Record.AuditId, entry.Record.Service, entry.Record.Actor, entry.Record.Action, entry.Record.SubjectType,
            entry.Record.SubjectId, entry.Record.Before, entry.Record.After, entry.Record.CorrelationId, entry.Record.OccurredAt,
            Convert.ToHexStringLower(entry.Hash));
    }
}
