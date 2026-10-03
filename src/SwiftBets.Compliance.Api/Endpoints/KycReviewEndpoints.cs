using SwiftBets.BuildingBlocks.Web;
using SwiftBets.Compliance.Application;
using SwiftBets.Compliance.Domain;
using SwiftBets.Contracts.Errors;
using SwiftBets.Contracts.Serialization;

namespace SwiftBets.Compliance.Api.Endpoints;

/// <summary>Document upload by the customer, the operators' review queue, signed file links, and the verified name for services.</summary>
public static class KycReviewEndpoints
{
    // Two files of up to 10 MB each plus the form fields.
    private const long MaxRequestBytes = (2 * KycUploadPolicy.MaxBytes) + (64 * 1024);

    public static IEndpointRouteBuilder MapKycReviewEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/me/kyc/documents", async (HttpContext context, KycReviewHandler review, TimeProvider time, CancellationToken cancellationToken) =>
        {
            if (!context.Request.HasFormContentType)
            {
                return Error.Validation("files_required", "Upload your identity document and a proof of address.").ToHttpResult(context);
            }

            var form = await context.Request.ReadFormAsync(cancellationToken);
            var typeText = form["documentType"].ToString();
            if (!Enum.TryParse<KycDocumentType>(typeText, ignoreCase: true, out var type) || !Enum.IsDefined(type) || int.TryParse(typeText, out _))
            {
                return Error.Validation("invalid_document", "Document type is idDocument or passport.").ToHttpResult(context);
            }

            var uploads = new List<KycUpload>();
            foreach (var (field, kind) in new[] { ("identity", KycFileKind.Identity), ("proofOfAddress", KycFileKind.ProofOfAddress) })
            {
                if (form.Files.GetFile(field) is not { } file)
                {
                    continue;
                }

                if (file.Length > KycUploadPolicy.MaxBytes)
                {
                    return Error.Validation("file_too_large", "Each file must be under 10 MB.").ToHttpResult(context);
                }

                using var buffer = new MemoryStream((int)file.Length);
                await file.CopyToAsync(buffer, cancellationToken);
                uploads.Add(new KycUpload(kind, buffer.ToArray()));
            }

            var number = form["documentNumber"].ToString().Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
            var result = await review.SubmitAsync(UserId(context), new KycDocument(type, number), form["legalName"].ToString(), uploads, cancellationToken);
            return result.IsSuccess ? Results.Json(ComplianceView.From(result.Value, time.GetUtcNow()), ContractJson.Options) : result.Error!.ToHttpResult(context);
        })
        .RequireAuthorization()
        .DisableAntiforgery()
        .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(MaxRequestBytes));

        var admin = endpoints.MapGroup("/admin/kyc");

        admin.MapGet("/queue", async (KycReviewHandler review, CancellationToken cancellationToken) =>
            Results.Json((await review.QueueAsync(100, cancellationToken)).Select(i => new
            {
                i.Case.CaseId,
                i.Case.UserId,
                i.Case.DocumentType,
                i.Case.DocumentHint,
                i.Case.LegalName,
                i.Case.CreatedAt,
                files = i.Files.Select(f => new { f.FileId, f.Kind, f.ContentType, f.SizeBytes }),
            }), ContractJson.Options))
            .RequireAuthorization(CompliancePermissions.Read);

        // Links expire in minutes, and the file route below also needs the read permission: a leaked link alone opens nothing.
        admin.MapGet("/cases/{caseId:guid}/files", async (Guid caseId, HttpContext context, KycReviewHandler review, CancellationToken cancellationToken) =>
        {
            var links = await review.LinksAsync(caseId, cancellationToken);
            return links.IsSuccess
                ? Results.Json(links.Value.Select(l => new { l.File.FileId, l.File.Kind, l.File.ContentType, url = $"/api/admin/kyc/files/{l.Token}", l.ExpiresAt }), ContractJson.Options)
                : links.Error!.ToHttpResult(context);
        })
        .RequireAuthorization(CompliancePermissions.Read);

        admin.MapGet("/files/{token}", async (string token, HttpContext context, KycReviewHandler review, CancellationToken cancellationToken) =>
        {
            if (await review.OpenAsync(token, cancellationToken) is not { } opened)
            {
                return Error.NotFound("link_expired", "This link has expired. Open the case again.").ToHttpResult(context);
            }

            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["Content-Security-Policy"] = "default-src 'none'; img-src 'self'; style-src 'unsafe-inline'; sandbox";
            return Results.Stream(opened.Content, opened.File.ContentType);
        })
        .RequireAuthorization(CompliancePermissions.Read);

        admin.MapPost("/cases/{caseId:guid}/approve", (Guid caseId, DecisionBody body, HttpContext context, KycReviewHandler review, TimeProvider time, CancellationToken cancellationToken) =>
            DecideAsync(caseId, true, body, context, review, time, cancellationToken))
            .RequireAuthorization(CompliancePermissions.Write);

        admin.MapPost("/cases/{caseId:guid}/reject", (Guid caseId, DecisionBody body, HttpContext context, KycReviewHandler review, TimeProvider time, CancellationToken cancellationToken) =>
            DecideAsync(caseId, false, body, context, review, time, cancellationToken))
            .RequireAuthorization(CompliancePermissions.Write);

        // Payments matches a bank account holder against the name on the verified case.
        endpoints.MapGet("/internal/users/{userId:guid}/kyc", async (Guid userId, KycReviewHandler review, CancellationToken cancellationToken) =>
        {
            var (status, legalName) = await review.VerifiedNameAsync(userId, cancellationToken);
            return Results.Json(new { status, legalName }, ContractJson.Options);
        })
        .RequireAuthorization(Roles.Service);

        return endpoints;
    }

    private static async Task<IResult> DecideAsync(Guid caseId, bool approve, DecisionBody body, HttpContext context, KycReviewHandler review, TimeProvider time, CancellationToken cancellationToken)
    {
        var operatorId = context.User.FindFirst("sub")?.Value ?? "operator";
        var result = await review.DecideAsync(caseId, approve, operatorId, body.Reason, cancellationToken);
        return result.IsSuccess ? Results.Json(ComplianceView.From(result.Value, time.GetUtcNow()), ContractJson.Options) : result.Error!.ToHttpResult(context);
    }

    private static Guid UserId(HttpContext context) =>
        Guid.TryParse(context.User.FindFirst("sub")?.Value, out var id) ? id : throw new BadHttpRequestException("Token subject is not a user id.", 401);

    public sealed record DecisionBody(string? Reason);
}
