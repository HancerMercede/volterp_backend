using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using EitherWay;
using FluentAssertions;
using Moq;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Interfaces;
using Volterp.Application.Services;
using Volterp.Domain.Entities;
using Volterp.Domain.Policies;
using Volterp.ElectronicInvoicing;
using Volterp.ElectronicInvoicing.Abstractions;
using Volterp.ElectronicInvoicing.Dgii;
using Volterp.ElectronicInvoicing.Xml;
using Xunit;

namespace Volterp.Tests.Services;

// The retrack of the documents the DGII is still validating: the verdict is
// asked again and, while it is not final, the next appointment is set with
// the backoff.
public class ElectronicInvoiceRetrackTests
{
    private const string IssuerRnc = "10100000101";

    private static readonly Guid CompanyId = Guid.Parse("0192b9c7-b6a3-7def-8c5e-1f2a3b4c5d6e");

    // The document was checked once, so the wait that follows grows.
    private static readonly TimeSpan Wait = TimeSpan.FromMinutes(2);

    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IElectronicInvoiceRepository> _invoices = new();
    private readonly Mock<ISigningCertificateProvider> _certificates = new();
    private readonly Mock<IEcfSigner> _signer = new();
    private readonly DgiiStub _dgii = new();

    public ElectronicInvoiceRetrackTests()
    {
        _unitOfWork.Setup(u => u.ElectronicInvoices).Returns(_invoices.Object);
        _unitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _signer.Setup(s => s.Sign(It.IsAny<string>(), It.IsAny<X509Certificate2>()))
            .Returns("<SemillaModel />");

        _certificates.Setup(c => c.GetSigningCertificateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCertificate());
    }

    [Fact]
    public async Task RetrackAsync_AsksTheDgiiAgainAboutTheDocumentsThatAreDue()
    {
        Given(Due());

        var result = await Service().RetrackAsync(CompanyId);

        result.Should().BeOfType<Either<Error, int>.Right>();
        ((Either<Error, int>.Right)result).Value.Should().Be(1);

        // The document is asked by its track id: it was sent, so the DGII
        // already has it.
        _dgii.StatusRequests.Should().Be(1);
        _dgii.StatusTrackId.Should().Be("track-1");
        _dgii.TrackIdRequests.Should().Be(0);
    }

    [Fact]
    public async Task RetrackAsync_AsksForTheDocumentsOfTheCompanyByHand()
    {
        Given(Due());

        await Service().RetrackAsync(CompanyId);

        // The retrack runs without an HTTP context, so the global tenant
        // filter is off: the company travels by hand.
        _invoices.Verify(i => i.GetDueForCheckAsync(CompanyId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RetrackAsync_WhenTheDgiiIsStillValidatingTheDocument_SchedulesTheNextCheck()
    {
        _dgii.Verdict = DgiiVerdict.InProcess;
        var invoice = Due();
        Given(invoice);

        var before = DateTime.UtcNow;
        await Service().RetrackAsync(CompanyId);
        var after = DateTime.UtcNow;

        invoice.Status.Should().Be(EcfStatus.InProcess);
        invoice.StatusCheckCount.Should().Be(2, "the check that was just made counts");
        invoice.NextCheckAt.Should().BeOnOrAfter(before.Add(Wait));
        invoice.NextCheckAt.Should().BeOnOrBefore(after.Add(Wait));
    }

    [Fact]
    public async Task RetrackAsync_WhenTheVerdictIsFinal_LeavesTheDocumentTerminalAndSchedulesNothing()
    {
        _dgii.Verdict = DgiiVerdict.Accepted;
        var invoice = Due();
        Given(invoice);

        await Service().RetrackAsync(CompanyId);

        invoice.Status.Should().Be(EcfStatus.Accepted);
        invoice.NextCheckAt.Should().BeNull("a final verdict is not asked again");
        invoice.StatusCheckCount.Should().Be(1);
    }

    [Fact]
    public async Task RetrackAsync_PersistsTheDocumentsItChanged()
    {
        Given(Due());

        await Service().RetrackAsync(CompanyId);

        // The documents are read without tracking, so the change is persisted
        // on purpose.
        _invoices.Verify(i => i.UpdateAsync(It.IsAny<ElectronicInvoice>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RetrackAsync_WithoutACompany_ReturnsLeft()
    {
        var result = await Service().RetrackAsync(Guid.Empty);

        result.Should().BeOfType<Either<Error, int>.Left>();
        _invoices.Verify(i => i.GetDueForCheckAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private void Given(ElectronicInvoice invoice)
    {
        _invoices.Setup(r => r.GetDueForCheckAsync(CompanyId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([invoice]);
    }

    private ElectronicInvoiceService Service()
    {
        var authenticator = new DgiiAuthenticator(_dgii, _signer.Object, new DgiiTokenCache());

        return new ElectronicInvoiceService(
            _unitOfWork.Object,
            new EcfXmlWriter(),
            _signer.Object,
            _certificates.Object,
            authenticator,
            _dgii,
            new VerdictCheckBackoff(TimeSpan.FromMinutes(1), TimeSpan.FromHours(4), 0.2, () => 0.5));
    }

    private static ElectronicInvoice Due() => new()
    {
        Id = Guid.CreateVersion7(),
        CompanyId = CompanyId,
        SaleId = Guid.CreateVersion7(),
        ENCF = "E320000000001",
        DocumentType = EcfDocumentType.Consumer,
        IssuerTaxId = IssuerRnc,
        TrackId = "track-1",
        Status = EcfStatus.InProcess,
        NextCheckAt = DateTime.UtcNow.AddMinutes(-1),
        StatusCheckCount = 1
    };

    private static X509Certificate2 CreateCertificate()
    {
        const string password = "volterp-test-password";

        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Volterp Test Emisor, C=DO",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(365));

        return X509CertificateLoader.LoadPkcs12(
            certificate.Export(X509ContentType.Pfx, password),
            password,
            X509KeyStorageFlags.EphemeralKeySet);
    }

    // The DGII as the tests see it: it answers the seed, the token and the
    // verdict, and it counts what it was asked.
    private sealed class DgiiStub : IDgiiClient
    {
        public DgiiVerdict Verdict { get; set; } = DgiiVerdict.Accepted;

        public int StatusRequests { get; private set; }
        public int TrackIdRequests { get; private set; }
        public string? StatusTrackId { get; private set; }

        public Task<string> GetSeedAsync(CancellationToken cancellationToken = default)
            => Task.FromResult("<SemillaModel><valor>abc</valor><fecha>2019-03-13T14:33:32-04:00</fecha></SemillaModel>");

        public Task<DgiiToken> RequestTokenAsync(string signedSeedXml, CancellationToken cancellationToken = default)
            => Task.FromResult(new DgiiToken("jwt", DateTimeOffset.UtcNow.AddHours(1), DateTimeOffset.UtcNow));

        public Task<DgiiReceipt> SendElectronicInvoiceAsync(string signedEcfXml, string token, CancellationToken cancellationToken = default)
            => Task.FromResult(new DgiiReceipt("track-1", null, null));

        public Task<DgiiStatus> GetStatusAsync(string trackId, string token, CancellationToken cancellationToken = default)
        {
            StatusRequests++;
            StatusTrackId = trackId;

            return Task.FromResult(new DgiiStatus(
                trackId,
                Verdict,
                Verdict.ToString(),
                IssuerRnc,
                "E320000000001",
                true,
                DateTimeOffset.UtcNow,
                []));
        }

        public Task<DgiiTrackLookup> GetTrackIdAsync(string issuerTaxId, string encf, string token, CancellationToken cancellationToken = default)
        {
            TrackIdRequests++;

            return Task.FromResult(new DgiiTrackLookup("track-1", "Aceptado", DateTimeOffset.UtcNow));
        }
    }
}
