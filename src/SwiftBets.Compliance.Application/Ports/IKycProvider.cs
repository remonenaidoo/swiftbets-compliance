using SwiftBets.Compliance.Domain;

namespace SwiftBets.Compliance.Application.Ports;

/// <summary>An identity-verification provider. Real providers decide later by webhook; the sandbox decides at once.</summary>
public interface IKycProvider
{
    string Name { get; }

    Task<KycDecision> VerifyAsync(Guid userId, KycDocument document, CancellationToken cancellationToken);
}

public interface IKycCaseReader
{
    Task<IReadOnlyList<KycCase>> CasesAsync(Guid userId, CancellationToken cancellationToken);
}
