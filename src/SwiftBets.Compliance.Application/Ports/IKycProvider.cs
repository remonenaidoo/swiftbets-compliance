using SwiftBets.Compliance.Domain;

namespace SwiftBets.Compliance.Application.Ports;

/// <summary>An identity-verification provider. Real providers decide later by webhook; the sandbox decides at once.</summary>
public interface IKycProvider
{
    string Name { get; }

    Task<KycDecision> VerifyAsync(Guid userId, KycDocument document, CancellationToken cancellationToken);
}

public interface IKycCaseReader
{
    Task<IReadOnlyList<KycCase>> CasesAsync(Guid userId, CancellationToken cancellationToken);

    Task<KycCase?> CaseAsync(Guid caseId, CancellationToken cancellationToken);

    /// <summary>Cases waiting for an operator, oldest first.</summary>
    Task<IReadOnlyList<KycCase>> AwaitingReviewAsync(int limit, CancellationToken cancellationToken);

    Task<IReadOnlyList<KycFile>> FilesAsync(Guid caseId, CancellationToken cancellationToken);

    Task<KycFile?> FileAsync(Guid fileId, CancellationToken cancellationToken);
}

/// <summary>
/// Object storage for uploaded files, keyed like an S3 bucket. An S3-compatible store or a filesystem volume sits
/// behind it; the database only ever holds the key.
/// </summary>
public interface IDocumentStore
{
    Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken);

    /// <summary>The stored bytes, or null if nothing is stored under the key.</summary>
    Task<Stream?> OpenAsync(string key, CancellationToken cancellationToken);
}

/// <summary>Short-lived signed links to one file, so a file address leaks nothing once it expires.</summary>
public interface IDocumentLinks
{
    string Sign(Guid fileId, DateTimeOffset expiresAt);

    /// <summary>The file id if the token is genuine and unexpired at <paramref name="now"/>.</summary>
    Guid? Verify(string token, DateTimeOffset now);
}
