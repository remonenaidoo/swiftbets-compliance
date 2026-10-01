UPDATE compliance.Restrictions SET EndAnnouncedAt = @Now
WHERE UserId = @UserId AND Kind IN (0, 1) AND EndsAt <= @Now AND EndAnnouncedAt IS NULL;
