namespace SwiftBets.Compliance.Domain;

public enum LimitKind
{
    Deposit,
    Stake,
    Loss,
}

/// <summary>Calendar periods in the jurisdiction time zone; a week starts on Monday.</summary>
public enum LimitPeriod
{
    Day,
    Week,
    Month,
}
