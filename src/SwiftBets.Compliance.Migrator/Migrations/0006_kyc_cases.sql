-- Identity-verification cases (E2 feature 5). Status: 0 not started, 1 pending, 2 verified, 3 rejected.
-- Only the document type and the last four characters are kept; the full number never reaches the database.
CREATE TABLE compliance.KycCases
(
    CaseId       uniqueidentifier  NOT NULL CONSTRAINT PK_KycCases PRIMARY KEY NONCLUSTERED,
    UserId       uniqueidentifier  NOT NULL,
    Provider     nvarchar(50)      NOT NULL,
    DocumentType tinyint           NOT NULL,
    DocumentHint nvarchar(4)       NOT NULL,
    Status       tinyint           NOT NULL,
    Reason       nvarchar(500)     NULL,
    CreatedAt    datetimeoffset(3) NOT NULL,
    DecidedAt    datetimeoffset(3) NULL
);
CREATE CLUSTERED INDEX IX_KycCases_User ON compliance.KycCases (UserId, CreatedAt);
