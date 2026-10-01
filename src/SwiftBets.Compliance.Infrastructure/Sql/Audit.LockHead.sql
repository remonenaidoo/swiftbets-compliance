SELECT Sequence, Hash FROM compliance.AuditHead WITH (UPDLOCK, ROWLOCK) WHERE Id = 1;
