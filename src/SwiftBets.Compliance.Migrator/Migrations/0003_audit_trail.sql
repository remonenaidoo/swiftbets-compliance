-- The audit trail for every service (E2 feature 1). Entries are append-only: the app role may insert and read but never
-- change or delete them, and each entry's hash chains to the one before, so tampering is detectable.
CREATE TABLE compliance.AuditEntries
(
    Sequence      bigint            NOT NULL CONSTRAINT PK_AuditEntries PRIMARY KEY,
    AuditId       uniqueidentifier  NOT NULL CONSTRAINT UQ_AuditEntries_AuditId UNIQUE,
    Service       nvarchar(100)     NOT NULL,
    Actor         nvarchar(200)     NOT NULL,
    Action        nvarchar(200)     NOT NULL,
    SubjectType   nvarchar(100)     NOT NULL,
    SubjectId     nvarchar(200)     NOT NULL,
    Before        nvarchar(max)     NULL,
    After         nvarchar(max)     NULL,
    CorrelationId nvarchar(100)     NOT NULL,
    OccurredAt    datetimeoffset(3) NOT NULL,
    RecordedAt    datetimeoffset(3) NOT NULL,
    PreviousHash  binary(32)        NOT NULL,
    Hash          binary(32)        NOT NULL
);
CREATE INDEX IX_AuditEntries_Subject ON compliance.AuditEntries (SubjectType, SubjectId, Sequence DESC);

-- One row: the end of the chain. Appends lock it, so entries are chained one at a time in a single order.
CREATE TABLE compliance.AuditHead
(
    Id       tinyint    NOT NULL CONSTRAINT PK_AuditHead PRIMARY KEY CONSTRAINT CK_AuditHead_Single CHECK (Id = 1),
    Sequence bigint     NOT NULL,
    Hash     binary(32) NOT NULL
);
INSERT INTO compliance.AuditHead (Id, Sequence, Hash) VALUES (1, 0, 0x0000000000000000000000000000000000000000000000000000000000000000);

DENY UPDATE, DELETE ON compliance.AuditEntries TO swiftbets_app;
