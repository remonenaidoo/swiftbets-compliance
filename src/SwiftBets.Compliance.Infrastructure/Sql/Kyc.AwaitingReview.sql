-- Status 1 is pending; only uploaded cases wait for an operator.
SELECT TOP (@Limit) CaseId, UserId, Provider, DocumentType, DocumentHint, Status, Reason, CreatedAt, DecidedAt, LegalName
FROM compliance.KycCases WHERE Status = 1 AND Provider = @Provider ORDER BY CreatedAt;
