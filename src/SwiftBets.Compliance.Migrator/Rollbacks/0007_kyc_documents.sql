-- Rolls back 0007_kyc_documents. File metadata and legal names go; the stored files must be removed from object storage separately.
DROP INDEX IF EXISTS IX_KycCases_Status ON compliance.KycCases;
DROP TABLE IF EXISTS compliance.KycFiles;
ALTER TABLE compliance.KycCases DROP COLUMN IF EXISTS LegalName;
