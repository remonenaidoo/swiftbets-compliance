namespace SwiftBets.Compliance.Domain;

/// <summary>Session time limit and reality-check interval; the gateway enforces them and the site shows the prompt.</summary>
public static class SessionSettings
{
    public static ComplianceError? Validate(int? sessionLimitMinutes, int? realityCheckMinutes) =>
        sessionLimitMinutes is < 15 or > 1440
            ? new ComplianceError("invalid_session_limit", "A session limit is 15 minutes to 24 hours.")
            : realityCheckMinutes is < 10 or > 240
                ? new ComplianceError("invalid_reality_check", "A reality check comes every 10 minutes to 4 hours.")
                : null;
}
