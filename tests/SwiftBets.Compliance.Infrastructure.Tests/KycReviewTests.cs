using Microsoft.Extensions.Options;
using SwiftBets.BuildingBlocks.Persistence;
using SwiftBets.BuildingBlocks.Testing;
using SwiftBets.Compliance.Application;
using SwiftBets.Compliance.Domain;
using SwiftBets.Compliance.Infrastructure.Documents;
using SwiftBets.Compliance.Infrastructure.Kyc;
using SwiftBets.Contracts.Messaging;

namespace SwiftBets.Compliance.Infrastructure.Tests;

public sealed class KycReviewTests(SqlServerFixture sql)
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4];
    private static readonly byte[] Pdf = [.. "%PDF-1.7 test"u8];

    private static async Task<(ComplianceDatabase Db, KycReviewHandler Review)> ReviewAsync(SqlServerFixture sql)
    {
        var db = await ComplianceDatabase.CreateAsync(sql);
        var options = Options.Create(new DocumentOptions { Root = Path.Combine(Path.GetTempPath(), "kyc-" + Guid.NewGuid().ToString("N")), SigningKey = new string('k', 40) });
        var reader = new SqlKycCaseReader(new SqlServerConnectionFactory(db.ConnectionString));
        return (db, new KycReviewHandler(db.Store, reader, new FileSystemDocumentStore(options), new HmacDocumentLinks(options), db.Time));
    }

    [Fact]
    public async Task Uploaded_documents_wait_for_an_operator_who_verifies_the_customer()
    {
        var (db, review) = await ReviewAsync(sql);
        var userId = Guid.NewGuid();

        var submitted = await review.SubmitAsync(userId, new KycDocument(KycDocumentType.IdDocument, "8001015009087"), "Thandi Mokoena",
            [new KycUpload(KycFileKind.Identity, Jpeg), new KycUpload(KycFileKind.ProofOfAddress, Pdf)], CancellationToken.None);
        var item = (await review.QueueAsync(10, CancellationToken.None)).ShouldHaveSingleItem();
        var links = (await review.LinksAsync(item.Case.CaseId, CancellationToken.None)).Value;
        var opened = await review.OpenAsync(links[0].Token, CancellationToken.None);
        var approved = await review.DecideAsync(item.Case.CaseId, true, "ops-1", "Documents match", CancellationToken.None);

        submitted.Value.KycStatus.ShouldBe(KycStatus.Pending);
        item.Files.Select(f => f.ContentType).ShouldBe(["image/jpeg", "application/pdf"]);
        opened.ShouldNotBeNull().File.Kind.ShouldBe(KycFileKind.Identity);
        await opened.Value.Content.DisposeAsync();
        approved.Value.KycStatus.ShouldBe(KycStatus.Verified);
        (await review.VerifiedNameAsync(userId, CancellationToken.None)).LegalName.ShouldBe("Thandi Mokoena");
        (await db.OutboxAsync(Topics.KycStatusChanged)).Count.ShouldBe(2);
        (await review.QueueAsync(10, CancellationToken.None)).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_file_that_is_not_an_image_or_pdf_is_refused_whatever_it_claims()
    {
        var (_, review) = await ReviewAsync(sql);

        var refused = await review.SubmitAsync(Guid.NewGuid(), new KycDocument(KycDocumentType.Passport, "A1234567"), "Thandi Mokoena",
            [new KycUpload(KycFileKind.Identity, "<html>not a photo</html>"u8.ToArray()), new KycUpload(KycFileKind.ProofOfAddress, Pdf)], CancellationToken.None);

        refused.Error!.Code.ShouldBe("file_type_not_allowed");
        (await review.QueueAsync(10, CancellationToken.None)).ShouldBeEmpty();
    }

    [Fact]
    public void A_signed_link_opens_until_it_expires_and_a_tampered_one_never_does()
    {
        var links = new HmacDocumentLinks(Options.Create(new DocumentOptions { SigningKey = new string('k', 40) }));
        var fileId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var token = links.Sign(fileId, now.AddMinutes(5));

        links.Verify(token, now).ShouldBe(fileId);
        links.Verify(token, now.AddMinutes(6)).ShouldBeNull();
        links.Verify(token[..^2] + (token[^2] == 'A' ? "BA" : "AA"), now).ShouldBeNull();
    }
}
