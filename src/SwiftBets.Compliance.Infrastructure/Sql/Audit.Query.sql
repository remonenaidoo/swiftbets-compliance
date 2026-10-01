SELECT TOP (@Limit) Sequence, AuditId, Service, Actor, Action, SubjectType, SubjectId, Before, After, CorrelationId, OccurredAt, PreviousHash, Hash
FROM compliance.AuditEntries
WHERE (@SubjectType IS NULL OR SubjectType = @SubjectType) AND (@SubjectId IS NULL OR SubjectId = @SubjectId)
ORDER BY Sequence DESC;
