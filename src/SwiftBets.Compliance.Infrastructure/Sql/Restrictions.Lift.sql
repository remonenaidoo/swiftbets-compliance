UPDATE compliance.Restrictions SET EndsAt = @Now, LiftedBy = @By WHERE RestrictionId = @RestrictionId AND (EndsAt IS NULL OR EndsAt > @Now);
UPDATE compliance.LiftRequests SET ApprovedBy = @By, ApprovedAt = @Now WHERE RequestId = @RequestId AND ApprovedAt IS NULL;
