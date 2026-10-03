MERGE compliance.KycCases AS target
USING (SELECT @CaseId AS CaseId) AS source ON target.CaseId = source.CaseId
WHEN MATCHED THEN UPDATE SET Status = @Status, Reason = @Reason, DecidedAt = @DecidedAt
WHEN NOT MATCHED THEN
    INSERT (CaseId, UserId, Provider, DocumentType, DocumentHint, Status, Reason, CreatedAt, DecidedAt, LegalName)
    VALUES (@CaseId, @UserId, @Provider, @DocumentType, @DocumentHint, @Status, @Reason, @CreatedAt, @DecidedAt, @LegalName);
