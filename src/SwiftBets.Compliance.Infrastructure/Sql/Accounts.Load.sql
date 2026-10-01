SELECT UserId, Revision, SessionLimitMinutes, RealityCheckMinutes, KycStatus FROM compliance.Accounts WHERE UserId = @UserId;
SELECT Kind, Period, Amount, Currency, PendingAmount, PendingEffectiveAt FROM compliance.Limits WHERE UserId = @UserId;
SELECT RestrictionId, Kind, StartsAt, EndsAt, Reason FROM compliance.Restrictions
WHERE UserId = @UserId AND (EndsAt IS NULL OR EndsAt > @Now);
