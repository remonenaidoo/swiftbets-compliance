SELECT TOP (@Batch) Sequence, AuditId, Service, Actor, Action, SubjectType, SubjectId, Before, After, CorrelationId, OccurredAt, PreviousHash, Hash
FROM compliance.AuditEntries
WHERE Sequence > @After
ORDER BY Sequence;
