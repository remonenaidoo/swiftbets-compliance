using SwiftBets.BuildingBlocks.Web;
using SwiftBets.Compliance.Application;
using SwiftBets.Compliance.Domain;
using SwiftBets.Contracts.Errors;
using SwiftBets.Contracts.Serialization;

namespace SwiftBets.Compliance.Api.Endpoints;

/// <summary>The operator console's customer-service actions. The staff user id from the token is the actor.</summary>
public static class CaseEndpoints
{
    public static IEndpointRouteBuilder MapCaseEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var user = endpoints.MapGroup("/admin/users/{userId:guid}");

        user.MapPost("/restrictions", async (Guid userId, RestrictionBody body, HttpContext context, CaseHandler cases, TimeProvider time, CancellationToken cancellationToken) =>
            Enum.TryParse<RestrictionKind>(body.Kind, ignoreCase: true, out var kind) && Enum.IsDefined(kind) && !int.TryParse(body.Kind, out _)
                ? (await cases.AddRestrictionAsync(userId, kind, body.Reason ?? string.Empty, Operator(context), cancellationToken)) is var result && result.IsSuccess
                    ? Results.Json(ComplianceView.From(result.Value, time.GetUtcNow()), ContractJson.Options)
                    : result.Error!.ToHttpResult(context)
                : Error.Validation("invalid_restriction", "Kind is noDeposits, noBetting, noWithdrawals or noMarketing.").ToHttpResult(context))
            .RequireAuthorization(CompliancePermissions.Write);

        user.MapPost("/restrictions/{restrictionId:guid}/lift", async (Guid userId, Guid restrictionId, ReasonBody body, HttpContext context, CaseHandler cases, CancellationToken cancellationToken) =>
            (await cases.RequestLiftAsync(userId, restrictionId, body.Reason ?? string.Empty, Operator(context), cancellationToken)).ToHttpResult(context, StatusCodes.Status202Accepted))
            .RequireAuthorization(CompliancePermissions.Write);

        user.MapGet("/lift-requests", async (Guid userId, CaseHandler cases, CancellationToken cancellationToken) =>
            Results.Json(await cases.PendingLiftsAsync(userId, cancellationToken), ContractJson.Options))
            .RequireAuthorization(CompliancePermissions.Read);

        user.MapPost("/lift-requests/{requestId:guid}/approve", async (Guid userId, Guid requestId, HttpContext context, CaseHandler cases, TimeProvider time, CancellationToken cancellationToken) =>
            (await cases.ApproveLiftAsync(userId, requestId, Operator(context), cancellationToken)) is var result && result.IsSuccess
                ? Results.Json(ComplianceView.From(result.Value, time.GetUtcNow()), ContractJson.Options)
                : result.Error!.ToHttpResult(context))
            .RequireAuthorization(CompliancePermissions.Write);

        user.MapGet("/notes", async (Guid userId, CaseHandler cases, CancellationToken cancellationToken) =>
            Results.Json(await cases.NotesAsync(userId, cancellationToken), ContractJson.Options))
            .RequireAuthorization(CompliancePermissions.Read);

        user.MapPost("/notes", async (Guid userId, NoteBody body, HttpContext context, CaseHandler cases, CancellationToken cancellationToken) =>
            (await cases.AddNoteAsync(userId, body.Body ?? string.Empty, Operator(context), cancellationToken)).ToHttpResult(context, StatusCodes.Status201Created))
            .RequireAuthorization(CompliancePermissions.Write);

        return endpoints;
    }

    private static string Operator(HttpContext context) =>
        context.User.FindFirst("sub")?.Value is { Length: > 0 } sub ? sub : throw new BadHttpRequestException("Token has no subject.", 401);

    public sealed record RestrictionBody(string? Kind, string? Reason);

    public sealed record ReasonBody(string? Reason);

    public sealed record NoteBody(string? Body);
}
