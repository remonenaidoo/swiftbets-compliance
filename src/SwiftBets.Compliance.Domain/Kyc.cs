namespace SwiftBets.Compliance.Domain;

public enum KycDocumentType
{
    IdDocument,
    Passport,
}

/// <summary>What the customer submits. Only the type and the last four characters are ever stored.</summary>
public sealed record KycDocument(KycDocumentType Type, string Number)
{
    public string Hint => Number.Length <= 4 ? Number : Number[^4..];
}

/// <summary>A verification case. LegalName is set when the customer uploads documents for an operator to review.</summary>
public sealed record KycCase(Guid CaseId, Guid UserId, string Provider, KycDocumentType DocumentType, string DocumentHint, KycStatus Status, string? Reason, DateTimeOffset CreatedAt, DateTimeOffset? DecidedAt, string? LegalName = null);

public sealed record KycDecision(bool Verified, string? Reason);

public static class KycPolicy
{
    public static ComplianceError? CanSubmit(KycStatus current) => current switch
    {
        KycStatus.Verified => new ComplianceError("already_verified", "This account is already verified."),
        KycStatus.Pending => new ComplianceError("kyc_pending", "A verification is already in progress."),
        _ => null,
    };

    /// <summary>A South African ID number is 13 digits with a Luhn check digit; a passport number is 6 to 9 letters or digits.</summary>
    public static ComplianceError? Validate(KycDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var valid = document.Type switch
        {
            KycDocumentType.IdDocument => document.Number.Length == 13 && document.Number.All(char.IsAsciiDigit) && Luhn(document.Number),
            _ => document.Number.Length is >= 6 and <= 9 && document.Number.All(char.IsAsciiLetterOrDigit),
        };
        return valid ? null : new ComplianceError("invalid_document", "That document number is not valid.");
    }

    private static bool Luhn(string digits)
    {
        var sum = 0;
        for (var i = 0; i < digits.Length; i++)
        {
            var d = digits[digits.Length - 1 - i] - '0';
            if (i % 2 == 1)
            {
                d = d * 2 > 9 ? (d * 2) - 9 : d * 2;
            }

            sum += d;
        }

        return sum % 10 == 0;
    }
}

/// <summary>Which file of an uploaded verification it is.</summary>
public enum KycFileKind : byte
{
    Identity = 1,
    ProofOfAddress = 2,
}

/// <summary>An uploaded file's metadata; the bytes live in object storage under StorageKey, never in the database.</summary>
public sealed record KycFile(Guid FileId, Guid CaseId, Guid UserId, KycFileKind Kind, string ContentType, long SizeBytes, string StorageKey, DateTimeOffset UploadedAt);

/// <summary>Rules for uploaded verification files and the name given with them.</summary>
public static class KycUploadPolicy
{
    public const string ReviewProvider = "manual";

    public const long MaxBytes = 10 * 1024 * 1024;

    /// <summary>The content type from the file's first bytes; the type the client claims is never trusted.</summary>
    public static string? Sniff(ReadOnlySpan<byte> head) =>
        head.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]) ? "image/jpeg"
        : head.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]) ? "image/png"
        : head.StartsWith("%PDF-"u8) ? "application/pdf"
        : null;

    public static ComplianceError? ValidateFile(long length, string? sniffed) =>
        length is <= 0 or > MaxBytes ? new ComplianceError("file_too_large", "Each file must be under 10 MB.")
        : sniffed is null ? new ComplianceError("file_type_not_allowed", "Upload a JPG, PNG or PDF file.")
        : null;

    public static ComplianceError? ValidateName(string? legalName) =>
        legalName is { Length: >= 3 and <= 200 } name && name.Trim().Contains(' ', StringComparison.Ordinal)
            ? null
            : new ComplianceError("legal_name_required", "Give your full name as it appears on your document.");
}
