using System.ComponentModel.DataAnnotations;

namespace SwiftBets.Compliance.Infrastructure.Documents;

public sealed class DocumentOptions
{
    public const string SectionName = "Documents";

    /// <summary>The folder the filesystem store writes under; in compose a named volume.</summary>
    [Required]
    public string Root { get; set; } = "/data/kyc-documents";

    /// <summary>Signs the short-lived links staff open files with; at least 32 characters, never logged.</summary>
    [Required]
    [MinLength(32)]
    public string SigningKey { get; set; } = string.Empty;
}
