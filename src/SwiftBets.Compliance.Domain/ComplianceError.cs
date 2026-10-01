namespace SwiftBets.Compliance.Domain;

/// <summary>A rule refused a change; Code is stable and becomes the API error code.</summary>
public sealed record ComplianceError(string Code, string Message);
