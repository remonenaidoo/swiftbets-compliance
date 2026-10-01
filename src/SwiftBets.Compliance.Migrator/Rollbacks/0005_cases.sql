-- Rolls back 0005_cases. DESTROYS notes and lift requests; the audit trail keeps the record of both.
DROP TABLE IF EXISTS compliance.Notes;
DROP TABLE IF EXISTS compliance.LiftRequests;
ALTER TABLE compliance.Restrictions DROP COLUMN IF EXISTS LiftedBy;
UPDATE compliance.Restrictions SET EndsAt = DATEADD(millisecond, 1, StartsAt) WHERE EndsAt = StartsAt;
ALTER TABLE compliance.Restrictions DROP CONSTRAINT CK_Restrictions_Period;
ALTER TABLE compliance.Restrictions ADD CONSTRAINT CK_Restrictions_Period CHECK (EndsAt IS NULL OR EndsAt > StartsAt);
