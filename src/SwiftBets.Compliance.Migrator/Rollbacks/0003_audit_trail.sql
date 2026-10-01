-- Rolls back 0003_audit_trail. DESTROYS the audit trail: only after exporting it, or restoring from a backup.
DROP TABLE IF EXISTS compliance.AuditHead;
DROP TABLE IF EXISTS compliance.AuditEntries;
