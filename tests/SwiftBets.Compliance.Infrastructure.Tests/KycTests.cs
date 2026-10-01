using SwiftBets.BuildingBlocks.Persistence;
using SwiftBets.BuildingBlocks.Testing;
using SwiftBets.Compliance.Application;
using SwiftBets.Compliance.Domain;
using SwiftBets.Compliance.Infrastructure.Kyc;
using SwiftBets.Contracts.Messaging;

namespace SwiftBets.Compliance.Infrastructure.Tests;

public sealed class KycTests(SqlServerFixture sql)
{
    private static async Task<(ComplianceDatabase Db, KycHandler Kyc)> KycAsync(SqlServerFixture sql)
    {
        var db = await ComplianceDatabase.CreateAsync(sql);
        return (db, new KycHandler(db.Store, new SandboxKycProvider(), new SqlKycCaseReader(new SqlServerConnectionFactory(db.ConnectionString)), db.Time));
    }

    [Fact]
    public async Task A_valid_document_is_verified_through_a_pending_case()
    {
        var (db, kyc) = await KycAsync(sql);
        var userId = Guid.NewGuid();

        var result = await kyc.SubmitAsync(userId, new KycDocument(KycDocumentType.IdDocument, "8001015009087"), CancellationToken.None);

        result.Value.KycStatus.ShouldBe(KycStatus.Verified);
        var kycCase = (await kyc.CasesAsync(userId, CancellationToken.None)).ShouldHaveSingleItem();
        (kycCase.Status, kycCase.DocumentHint, kycCase.Provider).ShouldBe((KycStatus.Verified, "9087", "sandbox"));
        (await db.OutboxAsync(Topics.KycStatusChanged)).Count.ShouldBe(2);
        (await kyc.SubmitAsync(userId, new KycDocument(KycDocumentType.Passport, "A1234567"), CancellationToken.None)).Error!.Code.ShouldBe("already_verified");
    }

    [Fact]
    public async Task A_rejected_customer_can_try_again()
    {
        var (_, kyc) = await KycAsync(sql);
        var userId = Guid.NewGuid();

        var rejected = await kyc.SubmitAsync(userId, new KycDocument(KycDocumentType.Passport, "B1230000"), CancellationToken.None);
        var retried = await kyc.SubmitAsync(userId, new KycDocument(KycDocumentType.Passport, "B1234567"), CancellationToken.None);

        rejected.Value.KycStatus.ShouldBe(KycStatus.Rejected);
        retried.Value.KycStatus.ShouldBe(KycStatus.Verified);
        (await kyc.CasesAsync(userId, CancellationToken.None)).Select(c => c.Status).ShouldBe([KycStatus.Verified, KycStatus.Rejected]);
    }

    [Fact]
    public async Task A_malformed_document_opens_no_case()
    {
        var (_, kyc) = await KycAsync(sql);
        var userId = Guid.NewGuid();

        (await kyc.SubmitAsync(userId, new KycDocument(KycDocumentType.IdDocument, "123"), CancellationToken.None)).Error!.Code.ShouldBe("invalid_document");
        (await kyc.CasesAsync(userId, CancellationToken.None)).ShouldBeEmpty();
    }
}
