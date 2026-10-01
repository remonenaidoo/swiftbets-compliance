UPDATE compliance.Accounts
SET Revision = @Revision, SessionLimitMinutes = @SessionLimitMinutes, RealityCheckMinutes = @RealityCheckMinutes, KycStatus = @KycStatus, UpdatedAt = @Now
WHERE UserId = @UserId;
