SELECT RequestId, UserId, RestrictionId, RequestedBy, Reason, RequestedAt
FROM compliance.LiftRequests
WHERE UserId = @UserId AND ApprovedAt IS NULL AND (@RequestId IS NULL OR RequestId = @RequestId)
ORDER BY RequestedAt;
