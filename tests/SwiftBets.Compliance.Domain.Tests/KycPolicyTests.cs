namespace SwiftBets.Compliance.Domain.Tests;

public sealed class KycPolicyTests
{
    [Theory]
    [InlineData(KycDocumentType.IdDocument, "8001015009087", true)]
    [InlineData(KycDocumentType.IdDocument, "8001015009088", false)]
    [InlineData(KycDocumentType.IdDocument, "800101500908", false)]
    [InlineData(KycDocumentType.Passport, "A1234567", true)]
    [InlineData(KycDocumentType.Passport, "A12-4567", false)]
    [InlineData(KycDocumentType.Passport, "A123", false)]
    public void Documents_are_checked_for_shape(KycDocumentType type, string number, bool valid) =>
        (KycPolicy.Validate(new KycDocument(type, number)) is null).ShouldBe(valid);

    [Fact]
    public void Only_the_last_four_characters_are_kept() =>
        new KycDocument(KycDocumentType.Passport, "A1234567").Hint.ShouldBe("4567");

    [Theory]
    [InlineData(KycStatus.Verified, "already_verified")]
    [InlineData(KycStatus.Pending, "kyc_pending")]
    public void A_verified_or_pending_account_cannot_submit_again(KycStatus status, string code) =>
        KycPolicy.CanSubmit(status)!.Code.ShouldBe(code);

    [Fact]
    public void A_rejected_account_may_try_again() => KycPolicy.CanSubmit(KycStatus.Rejected).ShouldBeNull();
}
