using System.Net;
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

// The durable queue of emission: the documents that are signed and waiting
// are taken with a lease, sent to the DGII and, when the DGII does not
// answer, they wait for the next attempt with the backoff.
public class ElectronicInvoiceQueueTests
{
    private const string IssuerRnc = "10100000101";

    private static readonly Guid CompanyId = Guid.Parse("0192b9c7-b6a3-7def-8c5e-1f2a3b4c5d6e");

    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IElectronicInvoiceRepository> _invoices = new();
    private readonly Mock<ISigningCertificateProvider> _certificates = new();
    private readonly Mock<IEcfSigner> _signer = new();
    private readonly DgiiStub _dgii = new();

    public ElectronicInvoiceQueueTests()
    {
        _unitOfWork.Setup(u => u.ElectronicInvoices).Returns(_invoices.Object);
        _unitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _certificates.Setup(c => c.GetSigningCertificateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCertificate());

        _signer.Setup(s => s.Sign(It.IsAny<string>(), It.IsAny<X509Certificate2>())).Returns("<ECF />");
    }

    [Fact]
    public async Task ProcessPendingAsync_SendsThePendingDocumentAndRecordsItsTrackId()
    {
        var invoice = Given(Pending());

        var result = await Service().ProcessPendingAsync(CompanyId);

        result.Should().BeOfType<Either<Error, int>.Right>();
        ((Either<Error, int>.Right)result).Value.Should().Be(1);

        _dgii.SendRequests.Should().Be(1);
        _dgii.SentXml.Should().Be("<ECF />");

        // The document is on record as received by the DGII, and it waits for
        // its verdict.
        invoice.Status.Should().Be(EcfStatus.Sent);
        invoice.TrackId.Should().Be("track-1");
        invoice.NextCheckAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessPendingAsync_TakesTheDocumentWithALeaseBeforeSendingIt()
    {
        Given(Pending());

        await Service().ProcessPendingAsync(CompanyId);

        // The document is taken for itself before it is sent, so a second
        // worker does not send it at the same time.
        _invoices.Verify(i => i.TryClaimAsync(
            CompanyId,
            It.IsAny<Guid>(),
            It.IsAny<string>(),
            It.IsAny<DateTime>(),
            It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessPendingAsync_WhenAnotherWorkerHasTheDocument_DoesNotSendIt()
    {
        var invoice = Given(Pending());
        _invoices.Setup(i => i.TryClaimAsync(
            CompanyId, It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await Service().ProcessPendingAsync(CompanyId);

        ((Either<Error, int>.Right)result).Value.Should().Be(0);
        _dgii.SendRequests.Should().Be(0);
        invoice.Status.Should().Be(EcfStatus.Signed);
    }

    [Fact]
    public async Task ProcessPendingAsync_WhenTheDgiiRefusesTheDocument_LeavesItRejectedWithoutAnotherAttempt()
    {
        var invoice = Given(Pending());
        _dgii.Refusal = "El archivo no cumple con el formato";

        await Service().ProcessPendingAsync(CompanyId);

        // The rejection of the DGII is final: the document is not pending
        // anymore and no attempt is scheduled.
        invoice.Status.Should().Be(EcfStatus.Rejected);
        invoice.VerdictNote.Should().Be("El archivo no cumple con el formato");
        invoice.NextAttemptAt.Should().BeNull();
    }

    [Fact]
    public async Task ProcessPendingAsync_WhenTheDgiiRejectsTheFile_LeavesItRejected()
    {
        var invoice = Given(Pending());
        _dgii.SendFailure = new DgiiException(
            "The DGII rejected the e-CF file (BadRequest).",
            "El archivo no cumple con el formato",
            statusCode: HttpStatusCode.BadRequest);

        await Service().ProcessPendingAsync(CompanyId);

        invoice.Status.Should().Be(EcfStatus.Rejected);
        invoice.VerdictNote.Should().Be("El archivo no cumple con el formato");
        invoice.NextAttemptAt.Should().BeNull();
    }

    [Fact]
    public async Task ProcessPendingAsync_WhenTheDgiiDoesNotAnswer_KeepsTheDocumentPending()
    {
        var invoice = Given(Pending());
        _dgii.SendFailure = new HttpRequestException("The DGII did not answer.");

        await Service().ProcessPendingAsync(CompanyId);

        // The document is still pending: the failure was a failure to talk to
        // the DGII, not a rejection of the document.
        invoice.Status.Should().Be(EcfStatus.Signed);
        invoice.TrackId.Should().BeNull();
        invoice.VerdictNote.Should().Contain("The DGII did not answer.");
    }

    [Fact]
    public async Task ProcessPendingAsync_WhenTheDgiiAnswersWithAServerFailure_KeepsTheDocumentPending()
    {
        var invoice = Given(Pending());
        _dgii.SendFailure = new DgiiException(
            "The DGII failed temporarily (ServiceUnavailable).",
            statusCode: HttpStatusCode.ServiceUnavailable);

        await Service().ProcessPendingAsync(CompanyId);

        invoice.Status.Should().Be(EcfStatus.Signed);
        invoice.NextAttemptAt.Should().NotBeNull();
        invoice.VerdictNote.Should().Contain("ServiceUnavailable");
    }

    [Fact]
    public async Task ProcessPendingAsync_WhenTheDgiiDoesNotAnswer_SchedulesTheNextAttemptWithTheBackoff()
    {
        var invoice = Given(Pending());
        _dgii.SendFailure = new HttpRequestException("The DGII did not answer.");

        var before = DateTime.UtcNow;
        await Service().ProcessPendingAsync(CompanyId);
        var after = DateTime.UtcNow;

        // The wait grows with the attempts the document already had, and it is
        // dispersed so a batch of documents does not hit the DGII at once.
        invoice.NextAttemptAt.Should().BeOnOrAfter(before.AddMinutes(1));
        invoice.NextAttemptAt.Should().BeOnOrBefore(after.AddMinutes(1));
    }

    [Fact]
    public async Task ProcessPendingAsync_WhenTheDocumentPassedTheWindow_DoesNotSendItAndFlagsItForReview()
    {
        var invoice = Given(Pending(createdAt: DateTime.UtcNow.AddHours(-73)));

        await Service().ProcessPendingAsync(CompanyId);

        // The window of the instructivo is the norm's, not the code's: a
        // document that went past it is not sent again on its own.
        _dgii.SendRequests.Should().Be(0);
        invoice.RequiresReview.Should().BeTrue();
        invoice.VerdictNote.Should().Contain("window");
    }

    [Fact]
    public async Task ProcessPendingAsync_OnTheFirstAttempt_SendsTheDocumentWithoutAskingTheDgiiByEncf()
    {
        Given(Pending(attemptCount: 1));

        await Service().ProcessPendingAsync(CompanyId);

        // There is nothing to recover: the document was never sent.
        _dgii.TrackIdRequests.Should().Be(0);
        _dgii.SendRequests.Should().Be(1);
    }

    [Fact]
    public async Task ProcessPendingAsync_WhenTheDocumentWasAlreadyAttempted_AsksTheDgiiWhetherItHasIt()
    {
        var invoice = Given(Pending(attemptCount: 2));

        await Service().ProcessPendingAsync(CompanyId);

        // The answer of the previous attempt may have been lost: the DGII is
        // asked before the document is sent again.
        _dgii.TrackIdRequests.Should().Be(1);
        _dgii.LookupEncf.Should().Be(invoice.ENCF);
    }

    [Fact]
    public async Task ProcessPendingAsync_WhenTheDgiiAlreadyHasTheDocument_AdoptsItsTrackIdWithoutSendingItAgain()
    {
        var invoice = Given(Pending(attemptCount: 2));

        await Service().ProcessPendingAsync(CompanyId);

        // The document was received and only the answer was lost: sending it
        // again would burn the number twice.
        _dgii.SendRequests.Should().Be(0);
        invoice.Status.Should().Be(EcfStatus.Sent);
        invoice.TrackId.Should().Be("track-1");
    }

    [Fact]
    public async Task ProcessPendingAsync_WithoutACompany_ReturnsLeft()
    {
        var result = await Service().ProcessPendingAsync(Guid.Empty);

        result.Should().BeOfType<Either<Error, int>.Left>();
        _invoices.Verify(i => i.GetPendingForEmissionAsync(
            It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private ElectronicInvoice Given(ElectronicInvoice invoice)
    {
        _invoices.Setup(i => i.GetPendingForEmissionAsync(
                CompanyId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([invoice]);

        _invoices.Setup(i => i.TryClaimAsync(
                CompanyId, It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _invoices.Setup(i => i.GetByCompanyAndIdAsync(CompanyId, invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);

        return invoice;
    }

    // The attempt count comes from the database after the claim: it counts
    // the attempt in progress.
    private static ElectronicInvoice Pending(int attemptCount = 1, DateTime? createdAt = null) => new()
    {
        Id = Guid.CreateVersion7(),
        CompanyId = CompanyId,
        SaleId = Guid.CreateVersion7(),
        ENCF = "E320000000001",
        DocumentType = EcfDocumentType.Consumer,
        IssuerTaxId = IssuerRnc,
        SignedXml = "<ECF />",
        Status = EcfStatus.Signed,
        AttemptCount = attemptCount,
        CreatedAt = createdAt ?? DateTime.UtcNow
    };

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
            retryBackoff: new EmissionRetryBackoff(TimeSpan.FromMinutes(1), TimeSpan.FromHours(4), 0.2, () => 0.5));
    }

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

    // The DGII as the tests see it: it answers the seed, the token, the
    // acknowledgement and the verdict, and it counts what it was asked.
    private sealed class DgiiStub : IDgiiClient
    {
        public string? Refusal { get; set; }

        public Exception? SendFailure { get; set; }

        public int SendRequests { get; private set; }
        public int TrackIdRequests { get; private set; }
        public string? SentXml { get; private set; }
        public string? LookupEncf { get; private set; }

        public Task<string> GetSeedAsync(CancellationToken cancellationToken = default)
            => Task.FromResult("<SemillaModel><valor>abc</valor><fecha>2019-03-13T14:33:32-04:00</fecha></SemillaModel>");

        public Task<DgiiToken> RequestTokenAsync(string signedSeedXml, CancellationToken cancellationToken = default)
            => Task.FromResult(new DgiiToken("jwt", DateTimeOffset.UtcNow.AddHours(1), DateTimeOffset.UtcNow));

        public Task<DgiiReceipt> SendElectronicInvoiceAsync(string signedEcfXml, string token, CancellationToken cancellationToken = default)
        {
            if (SendFailure is not null)
                throw SendFailure;

            SendRequests++;
            SentXml = signedEcfXml;

            return Task.FromResult(Refusal is null
                ? new DgiiReceipt("track-1", null, null)
                : new DgiiReceipt(null, Refusal, null));
        }

        public Task<DgiiStatus> GetStatusAsync(string trackId, string token, CancellationToken cancellationToken = default)
            => Task.FromResult(new DgiiStatus(
                trackId,
                DgiiVerdict.Accepted,
                "Aceptado",
                IssuerRnc,
                "E320000000001",
                true,
                DateTimeOffset.UtcNow,
                []));

        public Task<DgiiTrackLookup> GetTrackIdAsync(string issuerTaxId, string encf, string token, CancellationToken cancellationToken = default)
        {
            TrackIdRequests++;
            LookupEncf = encf;

            return Task.FromResult(new DgiiTrackLookup("track-1", "Aceptado", DateTimeOffset.UtcNow));
        }
    }
}
