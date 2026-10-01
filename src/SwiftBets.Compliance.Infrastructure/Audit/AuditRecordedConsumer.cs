using SwiftBets.BuildingBlocks.Messaging;
using SwiftBets.Compliance.Application.Audit;
using SwiftBets.Compliance.Domain.Audit;
using SwiftBets.Contracts.Audit;

namespace SwiftBets.Compliance.Infrastructure.Audit;

/// <summary>Chains every service's audit events into the one trail; a redelivered event is stored once.</summary>
public sealed class AuditRecordedConsumer(AuditTrailHandler trail) : IEventHandler<AuditRecordedV1>
{
    public Task HandleAsync(ConsumedEvent<AuditRecordedV1> message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        var e = message.Envelope.Payload;
        return trail.AppendAsync(new AuditRecord(e.AuditId, e.Service, e.Actor, e.Action, e.SubjectType, e.SubjectId, e.Before, e.After, e.CorrelationId, e.OccurredAt), cancellationToken);
    }
}
