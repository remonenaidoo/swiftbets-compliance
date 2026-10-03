-- Uploaded verification documents (BACKLOG 9). The files live in object storage; only their key and metadata are kept here.
-- LegalName is the full name the customer gives with the documents, which bank account checks match against.
ALTER TABLE compliance.KycCases ADD LegalName nvarchar(200) NULL;
GO

CREATE TABLE compliance.KycFiles
(
    FileId      uniqueidentifier  NOT NULL CONSTRAINT PK_KycFiles PRIMARY KEY NONCLUSTERED,
    CaseId      uniqueidentifier  NOT NULL,
    UserId      uniqueidentifier  NOT NULL,
    Kind        tinyint           NOT NULL,
    ContentType nvarchar(50)      NOT NULL,
    SizeBytes   bigint            NOT NULL,
    StorageKey  nvarchar(200)     NOT NULL,
    UploadedAt  datetimeoffset(3) NOT NULL
);
CREATE CLUSTERED INDEX IX_KycFiles_Case ON compliance.KycFiles (CaseId, Kind);
-- The review queue reads pending cases oldest first.
CREATE INDEX IX_KycCases_Status ON compliance.KycCases (Status, CreatedAt) INCLUDE (Provider);
