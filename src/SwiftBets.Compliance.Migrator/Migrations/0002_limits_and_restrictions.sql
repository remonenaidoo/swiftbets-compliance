-- One row per account that has ever had a responsible-gambling setting; it is the lock every change takes first.
CREATE TABLE compliance.Accounts
(
    UserId              uniqueidentifier  NOT NULL CONSTRAINT PK_Accounts PRIMARY KEY,
    Revision            bigint            NOT NULL,
    SessionLimitMinutes int               NULL,
    RealityCheckMinutes int               NULL,
    KycStatus           tinyint           NOT NULL CONSTRAINT DF_Accounts_KycStatus DEFAULT 0,
    UpdatedAt           datetimeoffset(3) NOT NULL
);

-- Kind: 0 deposit, 1 stake, 2 loss. Period: 0 day, 1 week, 2 month. A pending change with a null amount removes the limit.
CREATE TABLE compliance.Limits
(
    UserId             uniqueidentifier  NOT NULL CONSTRAINT FK_Limits_Accounts REFERENCES compliance.Accounts (UserId),
    Kind               tinyint           NOT NULL,
    Period             tinyint           NOT NULL,
    Amount             bigint            NOT NULL,
    Currency           char(3)           NOT NULL,
    PendingAmount      bigint            NULL,
    PendingEffectiveAt datetimeoffset(3) NULL,
    CONSTRAINT PK_Limits PRIMARY KEY (UserId, Kind, Period),
    CONSTRAINT CK_Limits_Amounts CHECK (Amount > 0 AND (PendingAmount IS NULL OR PendingAmount > 0)),
    CONSTRAINT CK_Limits_Pending CHECK (PendingAmount IS NULL OR PendingEffectiveAt IS NOT NULL)
);

-- Kind: 0 cooling-off, 1 self-exclusion, 2 no deposits, 3 no betting, 4 no withdrawals, 5 no marketing. Rows are never updated.
CREATE TABLE compliance.Restrictions
(
    RestrictionId uniqueidentifier  NOT NULL CONSTRAINT PK_Restrictions PRIMARY KEY NONCLUSTERED,
    UserId        uniqueidentifier  NOT NULL CONSTRAINT FK_Restrictions_Accounts REFERENCES compliance.Accounts (UserId),
    Kind          tinyint           NOT NULL,
    StartsAt      datetimeoffset(3) NOT NULL,
    EndsAt        datetimeoffset(3) NULL,
    Reason        nvarchar(500)     NOT NULL,
    CreatedBy     nvarchar(100)     NOT NULL,
    CONSTRAINT CK_Restrictions_Period CHECK (EndsAt IS NULL OR EndsAt > StartsAt)
);
CREATE CLUSTERED INDEX IX_Restrictions_User ON compliance.Restrictions (UserId, StartsAt);
