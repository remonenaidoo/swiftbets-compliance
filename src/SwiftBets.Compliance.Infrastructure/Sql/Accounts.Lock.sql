-- Creates the account row on first use, then holds it until the transaction ends, so changes to one account run in turn.
-- A racing first insert waits on the winner's row and then finds it; a range lock here would deadlock instead.
IF NOT EXISTS (SELECT 1 FROM compliance.Accounts WHERE UserId = @UserId)
BEGIN TRY
    INSERT INTO compliance.Accounts (UserId, Revision, UpdatedAt) VALUES (@UserId, 0, @Now);
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() NOT IN (2601, 2627) THROW;
END CATCH;
SELECT 1 FROM compliance.Accounts WITH (UPDLOCK, ROWLOCK) WHERE UserId = @UserId;
