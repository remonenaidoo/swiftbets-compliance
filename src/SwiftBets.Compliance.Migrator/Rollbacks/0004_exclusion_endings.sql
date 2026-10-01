-- Rolls back 0004_exclusion_endings. Endings already announced would be announced again after reapplying.
DROP INDEX IF EXISTS IX_Restrictions_Unannounced ON compliance.Restrictions;
ALTER TABLE compliance.Restrictions DROP COLUMN IF EXISTS EndAnnouncedAt;
