SELECT FileId, CaseId, UserId, Kind, ContentType, SizeBytes, StorageKey, UploadedAt FROM compliance.KycFiles WHERE FileId = @FileId;
