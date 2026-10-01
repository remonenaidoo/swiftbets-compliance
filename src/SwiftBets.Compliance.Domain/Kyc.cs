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

public sealed record KycCase(Guid CaseId, Guid UserId, string Provider, KycDocumentType DocumentType, string DocumentHint, KycStatus Status, string? Reason, DateTimeOffset CreatedAt, DateTimeOffset? DecidedAt);

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
