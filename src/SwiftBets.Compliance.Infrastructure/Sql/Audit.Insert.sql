INSERT INTO compliance.AuditEntries
    (Sequence, AuditId, Service, Actor, Action, SubjectType, SubjectId, Before, After, CorrelationId, OccurredAt, RecordedAt, PreviousHash, Hash)
VALUES
    (@Sequence, @AuditId, @Service, @Actor, @Action, @SubjectType, @SubjectId, @Before, @After, @CorrelationId, @OccurredAt, @RecordedAt, @PreviousHash, @Hash);
UPDATE compliance.AuditHead SET Sequence = @Sequence, Hash = @Hash WHERE Id = 1;
