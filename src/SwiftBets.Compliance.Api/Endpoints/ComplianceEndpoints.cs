using SwiftBets.BuildingBlocks.Web;
using SwiftBets.Compliance.Application;
using SwiftBets.Compliance.Domain;
using SwiftBets.Contracts.Errors;
using SwiftBets.Contracts.Results;
using SwiftBets.Contracts.Serialization;

namespace SwiftBets.Compliance.Api.Endpoints;

public static class ComplianceEndpoints
{
    private const string Self = "self";

    public static IEndpointRouteBuilder MapComplianceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var me = endpoints.MapGroup("/me").RequireAuthorization();

        me.MapGet("/compliance", async (HttpContext context, ComplianceHandler handler, TimeProvider time, CancellationToken cancellationToken) =>
            Results.Json(ComplianceView.From(await handler.GetAsync(UserId(context), cancellationToken), time.GetUtcNow()), ContractJson.Options));

        me.MapPut("/limits/{kind}/{period}", async (string kind, string period, LimitBody body, HttpContext context, ComplianceHandler handler, TimeProvider time, CancellationToken cancellationToken) =>
            !Parse<LimitKind>(kind, out var k) || !Parse<LimitPeriod>(period, out var p)
                ? NoSuchLimit(context)
                : body.Amount is null
                    ? Error.Validation("amount_required", "Give the limit in minor units; remove a limit with DELETE.").ToHttpResult(context)
                    : View(await handler.SetLimitAsync(UserId(context), k, p, body.Amount, body.Currency ?? string.Empty, Self, cancellationToken), context, time));

        me.MapDelete("/limits/{kind}/{period}", async (string kind, string period, HttpContext context, ComplianceHandler handler, TimeProvider time, CancellationToken cancellationToken) =>
        {
            if (!Parse<LimitKind>(kind, out var k) || !Parse<LimitPeriod>(period, out var p))
            {
                return NoSuchLimit(context);
            }

            var userId = UserId(context);
            var existing = (await handler.GetAsync(userId, cancellationToken)).Limit(k, p);
            return existing is null
                ? Error.NotFound("limit_not_found", "There is no such limit to remove.").ToHttpResult(context)
                : View(await handler.SetLimitAsync(userId, k, p, null, existing.Currency, Self, cancellationToken), context, time);
        });

        me.MapPost("/exclusions", async (ExclusionBody body, HttpContext context, ComplianceHandler handler, TimeProvider time, CancellationToken cancellationToken) =>
            Parse<RestrictionKind>(body.Kind ?? string.Empty, out var kind)
                ? View(await handler.StartExclusionAsync(UserId(context), kind, body.Days, body.Months, body.Reason ?? string.Empty, Self, cancellationToken), context, time)
                : Error.Validation("invalid_exclusion", "Kind is coolingOff or selfExclusion.").ToHttpResult(context));

        me.MapPut("/session-settings", async (SessionBody body, HttpContext context, ComplianceHandler handler, TimeProvider time, CancellationToken cancellationToken) =>
            View(await handler.SetSessionSettingsAsync(UserId(context), body.SessionLimitMinutes, body.RealityCheckMinutes, Self, cancellationToken), context, time));

        me.MapPost("/kyc", async (KycBody body, HttpContext context, KycHandler kyc, TimeProvider time, CancellationToken cancellationToken) =>
            Enum.TryParse<KycDocumentType>(body.DocumentType, ignoreCase: true, out var type) && Enum.IsDefined(type) && !int.TryParse(body.DocumentType, out _)
                ? View(await kyc.SubmitAsync(UserId(context), new KycDocument(type, (body.DocumentNumber ?? string.Empty).Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant()), cancellationToken), context, time)
                : Error.Validation("invalid_document", "Document type is idDocument or passport.").ToHttpResult(context));

        endpoints.MapGet("/admin/users/{userId:guid}/kyc-cases", async (Guid userId, KycHandler kyc, CancellationToken cancellationToken) =>
            Results.Json(await kyc.CasesAsync(userId, cancellationToken), ContractJson.Options))
            .RequireAuthorization(CompliancePermissions.Read);

        endpoints.MapGet("/admin/users/{userId:guid}/compliance", async (Guid userId, ComplianceHandler handler, TimeProvider time, CancellationToken cancellationToken) =>
            Results.Json(ComplianceView.From(await handler.GetAsync(userId, cancellationToken), time.GetUtcNow()), ContractJson.Options))
            .RequireAuthorization(CompliancePermissions.Read);

        return endpoints;
    }

    private static IResult View(Result<ComplianceState> result, HttpContext context, TimeProvider time) =>
        result.IsSuccess ? Results.Json(ComplianceView.From(result.Value, time.GetUtcNow()), ContractJson.Options) : result.Error!.ToHttpResult(context);

    private static IResult NoSuchLimit(HttpContext context) =>
        Error.NotFound("limit_not_found", "Limits are deposit, stake or loss, per day, week or month.").ToHttpResult(context);

    private static bool Parse<T>(string value, out T parsed)
        where T : struct, Enum => Enum.TryParse(value, ignoreCase: true, out parsed) && Enum.IsDefined(parsed) && !int.TryParse(value, out _);

    private static Guid UserId(HttpContext context) =>
        Guid.TryParse(context.User.FindFirst("sub")?.Value, out var id) ? id : throw new BadHttpRequestException("Token subject is not a user id.", 401);

    public sealed record LimitBody(long? Amount, string? Currency);

    public sealed record ExclusionBody(string? Kind, int? Days, int? Months, string? Reason);

    public sealed record KycBody(string? DocumentType, string? DocumentNumber);

    public sealed record SessionBody(int? SessionLimitMinutes, int? RealityCheckMinutes);
}
