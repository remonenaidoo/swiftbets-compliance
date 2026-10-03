using SwiftBets.Compliance.Application.Ports;
using SwiftBets.Compliance.Domain;
using SwiftBets.Contracts.Errors;
using SwiftBets.Contracts.Results;

namespace SwiftBets.Compliance.Application;

/// <summary>One uploaded file before it is stored: the bytes and the type sniffed from them.</summary>
public sealed record KycUpload(KycFileKind Kind, byte[] Content);

/// <summary>A case in the review queue with its files.</summary>
public sealed record KycReviewItem(KycCase Case, IReadOnlyList<KycFile> Files);

/// <summary>
/// Document verification by an operator: the customer uploads an identity document and proof of address, which opens
/// a Pending case; an operator approves or rejects it with a reason. Each step goes through the compliance store, so it
/// commits with KycStatusChangedV1, a new snapshot and an audit entry, exactly like a provider's decision.
/// </summary>
public sealed class KycReviewHandler(IComplianceStore store, IKycCaseReader cases, IDocumentStore documents, IDocumentLinks links, TimeProvider time)
{
    public static readonly TimeSpan LinkLifetime = TimeSpan.FromMinutes(5);

    public async Task<Result<ComplianceState>> SubmitAsync(Guid userId, KycDocument document, string? legalName, IReadOnlyList<KycUpload> uploads, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(uploads);
        if ((KycPolicy.Validate(document) ?? KycUploadPolicy.ValidateName(legalName)) is { } invalid)
        {
            return Error.Validation(invalid.Code, invalid.Message);
        }

        if (!uploads.Any(u => u.Kind == KycFileKind.Identity) || !uploads.Any(u => u.Kind == KycFileKind.ProofOfAddress))
        {
            return Error.Validation("files_required", "Upload your identity document and a proof of address.");
        }

        var sniffed = new List<(KycUpload Upload, string ContentType)>();
        foreach (var upload in uploads)
        {
            var type = KycUploadPolicy.Sniff(upload.Content.AsSpan(0, Math.Min(16, upload.Content.Length)));
            if (KycUploadPolicy.ValidateFile(upload.Content.Length, type) is { } badFile)
            {
                return Error.Validation(badFile.Code, badFile.Message);
            }

            sniffed.Add((upload, type!));
        }

        if (KycPolicy.CanSubmit((await store.GetAsync(userId, time.GetUtcNow(), cancellationToken)).KycStatus) is { } early)
        {
            return Error.BusinessRule(early.Code, early.Message);
        }

        // Files are stored before the case commits: a failed commit leaves an unreferenced file, never a case without files.
        var now = time.GetUtcNow();
        var caseId = Guid.CreateVersion7(now);
        var files = new List<KycFile>();
        foreach (var (upload, contentType) in sniffed)
        {
            var fileId = Guid.CreateVersion7(now);
            var key = $"kyc/{userId:N}/{caseId:N}/{fileId:N}";
            await using (var content = new MemoryStream(upload.Content, writable: false))
            {
                await documents.PutAsync(key, content, contentType, cancellationToken);
            }

            files.Add(new KycFile(fileId, caseId, userId, upload.Kind, contentType, upload.Content.Length, key, now));
        }

        return await store.ChangeAsync(userId, "self", now, state =>
        {
            if (KycPolicy.CanSubmit(state.KycStatus) is { } refused)
            {
                return Error.BusinessRule(refused.Code, refused.Message);
            }

            var kycCase = new KycCase(caseId, userId, KycUploadPolicy.ReviewProvider, document.Type, document.Hint, KycStatus.Pending, null, now, null, legalName!.Trim());
            return Result.Success(new ComplianceChange(state with { KycStatus = KycStatus.Pending }, "kyc.documents-submitted", [new KycChanged(kycCase, state.KycStatus), new KycFilesAdded(files)]));
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<KycReviewItem>> QueueAsync(int limit, CancellationToken cancellationToken)
    {
        var waiting = await cases.AwaitingReviewAsync(limit, cancellationToken);
        var items = new List<KycReviewItem>(waiting.Count);
        foreach (var kycCase in waiting)
        {
            items.Add(new KycReviewItem(kycCase, await cases.FilesAsync(kycCase.CaseId, cancellationToken)));
        }

        return items;
    }

    /// <summary>A signed link per file of the case, valid for <see cref="LinkLifetime"/>.</summary>
    public async Task<Result<IReadOnlyList<(KycFile File, string Token, DateTimeOffset ExpiresAt)>>> LinksAsync(Guid caseId, CancellationToken cancellationToken)
    {
        if (await cases.CaseAsync(caseId, cancellationToken) is null)
        {
            return Error.NotFound("case_not_found", "No such verification case.");
        }

        var expiresAt = time.GetUtcNow() + LinkLifetime;
        IReadOnlyList<(KycFile, string, DateTimeOffset)> signed = [.. (await cases.FilesAsync(caseId, cancellationToken)).Select(f => (f, links.Sign(f.FileId, expiresAt), expiresAt))];
        return Result.Success(signed);
    }

    /// <summary>The file behind a signed link, or null when the link is forged, expired or the file is gone.</summary>
    public async Task<(KycFile File, Stream Content)?> OpenAsync(string token, CancellationToken cancellationToken)
    {
        if (links.Verify(token, time.GetUtcNow()) is not { } fileId || await cases.FileAsync(fileId, cancellationToken) is not { } file)
        {
            return null;
        }

        return await documents.OpenAsync(file.StorageKey, cancellationToken) is { } content ? (file, content) : null;
    }

    public async Task<Result<ComplianceState>> DecideAsync(Guid caseId, bool approve, string operatorId, string? reason, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 400)
        {
            return Error.Validation("reason_required", "Give a reason for the decision (up to 400 characters).");
        }

        if (await cases.CaseAsync(caseId, cancellationToken) is not { } pending)
        {
            return Error.NotFound("case_not_found", "No such verification case.");
        }

        if (pending.Status != KycStatus.Pending || pending.Provider != KycUploadPolicy.ReviewProvider)
        {
            return Error.Conflict("case_decided", "This case is not waiting for review.");
        }

        return await store.ChangeAsync(pending.UserId, operatorId, time.GetUtcNow(), state =>
        {
            if (state.KycStatus != KycStatus.Pending)
            {
                return Error.Conflict("case_decided", "This case is not waiting for review.");
            }

            var status = approve ? KycStatus.Verified : KycStatus.Rejected;
            var decided = pending with { Status = status, Reason = reason.Trim(), DecidedAt = time.GetUtcNow() };
            return Result.Success(new ComplianceChange(state with { KycStatus = status }, approve ? "kyc.approved" : "kyc.rejected", [new KycChanged(decided, KycStatus.Pending)]));
        }, cancellationToken);
    }

    /// <summary>The name on the customer's verified case, for services that must match it (bank account checks).</summary>
    public async Task<(KycStatus Status, string? LegalName)> VerifiedNameAsync(Guid userId, CancellationToken cancellationToken)
    {
        var all = await cases.CasesAsync(userId, cancellationToken);
        var verified = all.FirstOrDefault(c => c.Status == KycStatus.Verified);
        return (verified?.Status ?? (all.Count > 0 ? all[0].Status : KycStatus.NotStarted), verified?.LegalName);
    }
}
