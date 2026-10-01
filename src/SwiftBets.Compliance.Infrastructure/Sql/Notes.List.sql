SELECT TOP (200) NoteId, UserId, Author, Body, CreatedAt FROM compliance.Notes WHERE UserId = @UserId ORDER BY CreatedAt DESC;
