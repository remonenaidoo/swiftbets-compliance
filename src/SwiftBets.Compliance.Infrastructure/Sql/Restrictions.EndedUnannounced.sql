-- Kind 0 cooling-off, 1 self-exclusion.
SELECT DISTINCT TOP (@Batch) UserId
FROM compliance.Restrictions
WHERE Kind IN (0, 1) AND EndsAt <= @Now AND EndAnnouncedAt IS NULL;
