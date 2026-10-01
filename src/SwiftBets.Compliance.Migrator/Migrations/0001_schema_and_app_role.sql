IF SCHEMA_ID(N'compliance') IS NULL EXEC (N'CREATE SCHEMA compliance');
IF DATABASE_PRINCIPAL_ID(N'swiftbets_app') IS NULL EXEC (N'CREATE ROLE swiftbets_app');
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::compliance TO swiftbets_app;
-- Compliance publishes its events through the shared outbox (created by the outbox migrations that run first).
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::outbox TO swiftbets_app;
