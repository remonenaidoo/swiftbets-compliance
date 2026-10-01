SELECT CaseId, UserId, Provider, DocumentType, DocumentHint, Status, Reason, CreatedAt, DecidedAt
FROM compliance.KycCases WHERE UserId = @UserId ORDER BY CreatedAt DESC;
