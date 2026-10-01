-- Customer-service records: who lifted a restriction, four-eyes lift requests, and operator notes (E2 feature 6).
ALTER TABLE compliance.Restrictions ADD LiftedBy nvarchar(100) NULL;
-- A block lifted the moment it was placed ends when it starts, and is simply never active.
ALTER TABLE compliance.Restrictions DROP CONSTRAINT CK_Restrictions_Period;
ALTER TABLE compliance.Restrictions ADD CONSTRAINT CK_Restrictions_Period CHECK (EndsAt IS NULL OR EndsAt >= StartsAt);

CREATE TABLE compliance.LiftRequests
(
    RequestId     uniqueidentifier  NOT NULL CONSTRAINT PK_LiftRequests PRIMARY KEY,
    UserId        uniqueidentifier  NOT NULL,
    RestrictionId uniqueidentifier  NOT NULL CONSTRAINT FK_LiftRequests_Restrictions REFERENCES compliance.Restrictions (RestrictionId),
    RequestedBy   nvarchar(100)     NOT NULL,
    Reason        nvarchar(500)     NOT NULL,
    RequestedAt   datetimeoffset(3) NOT NULL,
    ApprovedBy    nvarchar(100)     NULL,
    ApprovedAt    datetimeoffset(3) NULL,
    CONSTRAINT CK_LiftRequests_FourEyes CHECK (ApprovedBy IS NULL OR ApprovedBy <> RequestedBy)
);
-- At most one request waits per restriction.
CREATE UNIQUE INDEX UX_LiftRequests_Pending ON compliance.LiftRequests (RestrictionId) WHERE ApprovedAt IS NULL;
CREATE INDEX IX_LiftRequests_User ON compliance.LiftRequests (UserId, RequestedAt);

CREATE TABLE compliance.Notes
(
    NoteId    uniqueidentifier  NOT NULL CONSTRAINT PK_Notes PRIMARY KEY NONCLUSTERED,
    UserId    uniqueidentifier  NOT NULL,
    Author    nvarchar(100)     NOT NULL,
    Body      nvarchar(4000)    NOT NULL,
    CreatedAt datetimeoffset(3) NOT NULL
);
CREATE CLUSTERED INDEX IX_Notes_User ON compliance.Notes (UserId, CreatedAt);
