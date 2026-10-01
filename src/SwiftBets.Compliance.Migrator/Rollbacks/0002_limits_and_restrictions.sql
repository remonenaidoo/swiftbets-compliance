-- Rolls back 0002_limits_and_restrictions. DESTROYS every limit and restriction: only for a database with no customers yet,
-- or after restoring from a backup taken before the migration.
DROP TABLE IF EXISTS compliance.Restrictions;
DROP TABLE IF EXISTS compliance.Limits;
DROP TABLE IF EXISTS compliance.Accounts;
