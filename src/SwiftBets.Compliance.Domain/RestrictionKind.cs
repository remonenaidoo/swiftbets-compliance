namespace SwiftBets.Compliance.Domain;

public enum RestrictionKind
{
    CoolingOff,
    SelfExclusion,
    NoDeposits,
    NoBetting,
    NoWithdrawals,
    NoMarketing,
}

public enum KycStatus
{
    NotStarted,
    Pending,
    Verified,
    Rejected,
}
