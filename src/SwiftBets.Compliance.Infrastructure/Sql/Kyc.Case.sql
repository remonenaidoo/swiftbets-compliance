SELECT CaseId, UserId, Provider, DocumentType, DocumentHint, Status, Reason, CreatedAt, DecidedAt, LegalName
FROM compliance.KycCases WHERE CaseId = @CaseId;
