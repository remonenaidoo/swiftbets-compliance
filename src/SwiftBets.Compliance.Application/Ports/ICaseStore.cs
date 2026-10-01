using SwiftBets.Compliance.Domain;

namespace SwiftBets.Compliance.Application.Ports;

/// <summary>Customer-service records beside the compliance state: notes and pending lift requests. Each write is audited.</summary>
public interface ICaseStore
{
    Task<Note> AddNoteAsync(Guid userId, string author, string body, DateTimeOffset now, CancellationToken cancellationToken);

    Task<IReadOnlyList<Note>> NotesAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>False when the restriction already has a pending request.</summary>
    Task<bool> AddLiftRequestAsync(LiftRequest request, CancellationToken cancellationToken);

    Task<LiftRequest?> PendingLiftRequestAsync(Guid userId, Guid requestId, CancellationToken cancellationToken);

    Task<IReadOnlyList<LiftRequest>> PendingLiftRequestsAsync(Guid userId, CancellationToken cancellationToken);
}

public sealed record Note(Guid NoteId, Guid UserId, string Author, string Body, DateTimeOffset CreatedAt);
