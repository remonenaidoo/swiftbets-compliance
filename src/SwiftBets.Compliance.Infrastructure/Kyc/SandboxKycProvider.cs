using SwiftBets.Compliance.Application.Ports;
using SwiftBets.Compliance.Domain;

namespace SwiftBets.Compliance.Infrastructure.Kyc;

/// <summary>
/// The sandbox verifier for every environment until a real provider is contracted: it accepts any well-formed document
/// and rejects numbers ending in 0000, so tests and demos can drive both outcomes.
/// </summary>
public sealed class SandboxKycProvider : IKycProvider
{
    public string Name => "sandbox";

    public Task<KycDecision> VerifyAsync(Guid userId, KycDocument document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        return Task.FromResult(document.Number.EndsWith("0000", StringComparison.Ordinal)
            ? new KycDecision(false, "document could not be matched (sandbox)")
            : new KycDecision(true, null));
    }
}
