SELECT FileId, CaseId, UserId, Kind, ContentType, SizeBytes, StorageKey, UploadedAt FROM compliance.KycFiles WHERE CaseId = @CaseId ORDER BY Kind;
