-- When an exclusion's period runs out, compliance announces it once (a new snapshot), so identity can reopen the account.
ALTER TABLE compliance.Restrictions ADD EndAnnouncedAt datetimeoffset(3) NULL;
GO
CREATE INDEX IX_Restrictions_Unannounced ON compliance.Restrictions (EndsAt) INCLUDE (UserId, Kind) WHERE EndAnnouncedAt IS NULL AND EndsAt IS NOT NULL;
