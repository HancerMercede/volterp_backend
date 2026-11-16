using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Net;
using System.Net.Http.Headers;
using System.Xml.Linq;
using EitherWay;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Volterp.Api.Helpers;
using Volterp.Application.DTOs.ElectronicInvoiceDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Interfaces;
using Volterp.Application.Services;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.ElectronicInvoicing;
using Volterp.ElectronicInvoicing.Abstractions;
using Volterp.ElectronicInvoicing.Dgii;
using Volterp.ElectronicInvoicing.Xml;
using Volterp.Infrastructure.Data;
using Volterp.Tests.Integration;
using Volterp.Tests.TestHelpers;
using Xunit;

namespace Volterp.Tests.Services;

// The orchestrator of the emission: a completed sale becomes a signed
// document that waits in the durable queue, and the queue is the one that
// talks to the DGII.
public class ElectronicInvoiceServiceTests
{
    // The signer of the tests answers with a document that carries a
    // signature, because the security code is taken out of it.
    private const string SignedXml = "<ECF Signed=\"true\"><Signature xmlns=\"http://www.w3.org/2000/09/xmldsig#\"><SignatureValue>BoXjC8ZmFrQnVuZXZlck9yaWdpbmFs</SignatureValue></Signature></ECF>";
    private const string IssuerRnc = "10100000101";

    private static readonly Guid CompanyId = Guid.Parse("0192b9c7-b6a3-7def-8c5e-1f2a3b4c5d6e");
    private static readonly Guid OtherCompanyId = Guid.Parse("0192b9c7-b6a3-7def-8c5e-1f2a3b4c5d6f");
    private static readonly Guid SaleId = Guid.Parse("0192b9c7-b6a3-7def-8c5e-1f2a3b4c5d74");

    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ISaleRepository> _sales = new();
    private readonly Mock<ICompanyRepository> _companies = new();
    private readonly Mock<IElectronicInvoiceRepository> _invoices = new();
    private readonly Mock<IElectronicInvoiceSequenceRepository> _sequences = new();
    private readonly Mock<IEcfSigner> _signer = new();
    private readonly Mock<ISigningCertificateProvider> _certificates = new();
    private readonly DgiiStub _dgii = new();
    private readonly List<string> _signedXmls = [];
    private readonly List<X509Certificate2> _signedWith = [];

    private ElectronicInvoice? _persisted;

    public ElectronicInvoiceServiceTests()
    {
        _unitOfWork.Setup(u => u.Sales).Returns(_sales.Object);
        _unitOfWork.Setup(u => u.Companies).Returns(_companies.Object);
        _unitOfWork.Setup(u => u.ElectronicInvoices).Returns(_invoices.Object);
        _unitOfWork.Setup(u => u.ElectronicInvoiceSequences).Returns(_sequences.Object);
        _unitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _signer.Setup(s => s.Sign(It.IsAny<string>(), It.IsAny<X509Certificate2>()))
            .Callback((string xml, X509Certificate2 certificate) =>
            {
                _signedXmls.Add(xml);
                _signedWith.Add(certificate);
            })
            .Returns(SignedXml);

        _invoices.Setup(i => i.AddAsync(It.IsAny<ElectronicInvoice>(), It.IsAny<CancellationToken>()))
            .Callback((ElectronicInvoice invoice, CancellationToken _) => _persisted = invoice)
            .Returns(Task.CompletedTask);

        _certificates.Setup(c => c.GetSigningCertificateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCertificate());
    }

    [Fact]
    public async Task IssueAsync_WithACompletedSale_PersistsTheDocumentPendingAndDoesNotCallTheDgii()
    {
        Given(Sale(), Company());

        var result = await Service().IssueAsync(SaleId, CompanyId);

        result.Should().BeOfType<Either<Error, ElectronicInvoiceDto>.Right>();
        var dto = ((Either<Error, ElectronicInvoiceDto>.Right)result).Value;
        dto.ENCF.Should().Be("E320000000001");

        // The document waits in the queue: it is signed and persisted, and
        // the verdict is not here yet.
        dto.Status.Should().Be(EcfStatus.Signed);
        dto.TrackId.Should().BeNull();

        // The number comes from the sequence of the issuer, and it is the only
        // one that is taken.
        _sequences.Verify(s => s.NextSequenceAsync(CompanyId, EcfDocumentType.Consumer, It.IsAny<CancellationToken>()), Times.Once);

        // The document that was signed carries the number the DGII will read.
        _signedXmls.Should().Contain(xml => xml.Contains("<eNCF>E320000000001</eNCF>"));

        // The send is the queue's job, not the issue's.
        _dgii.SendRequests.Should().Be(0);

        // The document is on record, signed and waiting for the queue.
        _persisted.Should().NotBeNull();
        _persisted!.CompanyId.Should().Be(CompanyId);
        _persisted.SaleId.Should().Be(SaleId);
        _persisted.ENCF.Should().Be("E320000000001");
        _persisted.DocumentType.Should().Be(EcfDocumentType.Consumer);
        _persisted.IssuerTaxId.Should().Be(IssuerRnc);
        _persisted.SignedXml.Should().Be(SignedXml);
        _persisted.TrackId.Should().BeNull();
        _persisted.Status.Should().Be(EcfStatus.Signed);

        // The security code is taken out of the signature, so it is on record
        // with the document.
        _persisted.SecurityCode.Should().Be("BoXjC8");
    }

    [Fact]
    public async Task IssueAsync_WhenTheSaleAlreadyHasADocument_DoesNotAllocateANumberAndDoesNotSend()
    {
        Given(Sale(), Company());
        GivenDocument(EcfStatus.Accepted, "E320000000009");

        var result = await Service().IssueAsync(SaleId, CompanyId);

        result.Should().BeOfType<Either<Error, ElectronicInvoiceDto>.Right>();
        var dto = ((Either<Error, ElectronicInvoiceDto>.Right)result).Value;
        dto.ENCF.Should().Be("E320000000009");

        // No second e-NCF is taken and nothing goes to the DGII twice.
        _sequences.Verify(s => s.NextSequenceAsync(It.IsAny<Guid>(), It.IsAny<EcfDocumentType>(), It.IsAny<CancellationToken>()), Times.Never);
        _dgii.SendRequests.Should().Be(0);
        _persisted.Should().BeNull();
    }

    [Fact]
    public async Task IssueAsync_WhenTheExistingDocumentNeedsSignature_ReturnsLeft()
    {
        Given(Sale(), Company());
        _invoices.Setup(r => r.GetBySaleIdAsync(SaleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ElectronicInvoice
            {
                Id = Guid.CreateVersion7(),
                CompanyId = CompanyId,
                SaleId = SaleId,
                ENCF = "E320000000001",
                DocumentType = EcfDocumentType.Consumer,
                IssuerTaxId = IssuerRnc,
                Status = EcfStatus.Draft,
                RequiresReview = true
            });

        var result = await Service().IssueAsync(SaleId, CompanyId);

        result.Should().BeOfType<Either<Error, ElectronicInvoiceDto>.Left>();
        var error = ((Either<Error, ElectronicInvoiceDto>.Left)result).Value;
        error.Message.Should().Contain("pending signature");

        _sequences.Verify(s => s.NextSequenceAsync(It.IsAny<Guid>(), It.IsAny<EcfDocumentType>(), It.IsAny<CancellationToken>()), Times.Never);
        _dgii.SendRequests.Should().Be(0);
    }

    [Fact]
    public async Task IssueAsync_WithASaleOfAnotherCompany_ReturnsLeft()
    {
        Given(Sale(companyId: OtherCompanyId), Company());

        var result = await Service().IssueAsync(SaleId, CompanyId);

        result.Should().BeOfType<Either<Error, ElectronicInvoiceDto>.Left>();
        var error = ((Either<Error, ElectronicInvoiceDto>.Left)result).Value;
        error.Message.Should().Contain("company");

        // The sale of another company is not issued by this company: no
        // number is taken and nothing reaches the DGII.
        _sequences.Verify(s => s.NextSequenceAsync(It.IsAny<Guid>(), It.IsAny<EcfDocumentType>(), It.IsAny<CancellationToken>()), Times.Never);
        _dgii.SendRequests.Should().Be(0);
        _persisted.Should().BeNull();
    }

    [Fact]
    public async Task IssueAsync_WithoutAnActiveCertificateOfTheIssuer_LeavesTheDocumentForReview()
    {
        Given(Sale(), Company());
        _certificates.Setup(c => c.GetSigningCertificateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(
                $"There is no active signing certificate for the issuer RNC {IssuerRnc}."));

        var result = await Service().IssueAsync(SaleId, CompanyId);

        result.Should().BeOfType<Either<Error, ElectronicInvoiceDto>.Left>();
        var error = ((Either<Error, ElectronicInvoiceDto>.Left)result).Value;
        error.Message.Should().Contain("no active signing certificate");

        // The certificate is asked for by the RNC of the issuer, and without
        // it the document is not signed: there is no fallback.
        _certificates.Verify(c => c.GetSigningCertificateAsync(IssuerRnc, It.IsAny<CancellationToken>()), Times.Once);
        _dgii.SendRequests.Should().Be(0);

        // The failure is not lost: the document stays on record for a human.
        _persisted.Should().NotBeNull();
        _persisted!.Status.Should().Be(EcfStatus.Draft);
        _persisted.RequiresReview.Should().BeTrue();
        _persisted.VerdictNote.Should().Contain("no active signing certificate");
        _persisted.SignedXml.Should().BeEmpty();
    }

    [Fact]
    public async Task IssueAsync_WhenTheDgiiRefusesTheDocument_LeavesTheRefusalToTheQueue()
    {
        Given(Sale(), Company());
        _dgii.Refusal = "El archivo no cumple con el formato";

        var result = await Service().IssueAsync(SaleId, CompanyId);

        // The issue does not talk to the DGII: the document leaves pending
        // and the refusal of the DGII is the queue's business.
        result.Should().BeOfType<Either<Error, ElectronicInvoiceDto>.Right>();
        ((Either<Error, ElectronicInvoiceDto>.Right)result).Value.Status.Should().Be(EcfStatus.Signed);
        _dgii.SendRequests.Should().Be(0);

        // The queue sends the document and the refusal of the DGII lands on it.
        GivenPending(_persisted!);
        await Service().ProcessPendingAsync(CompanyId);

        _dgii.SendRequests.Should().Be(1);
        _persisted!.Status.Should().Be(EcfStatus.Rejected);
        _persisted.VerdictNote.Should().Be("El archivo no cumple con el formato");
    }

    [Fact]
    public async Task IssueAsync_DoesNotAskTheDgiiForAVerdict()
    {
        Given(Sale(), Company());
        _dgii.Verdict = DgiiVerdict.InProcess;

        var result = await Service().IssueAsync(SaleId, CompanyId);

        result.Should().BeOfType<Either<Error, ElectronicInvoiceDto>.Right>();
        var dto = ((Either<Error, ElectronicInvoiceDto>.Right)result).Value;
        dto.Status.Should().Be(EcfStatus.Signed);

        // Nobody asked about the document yet: it has no track id, and the
        // verdict of the DGII is asked by the queue.
        _dgii.StatusRequests.Should().Be(0);
        _persisted.Should().NotBeNull();
        _persisted!.Status.Should().Be(EcfStatus.Signed);
        _persisted.TrackId.Should().BeNull();
    }

    [Fact]
    public async Task IssueAsync_WhenTheMapperFailsAfterTakingTheNumber_LeavesTheDocumentForReview()
    {
        var sale = Sale(EcfDocumentType.FiscalCredit);
        sale.ReceptorTaxId = "101010101";
        sale.ClienteName = "Cliente de Prueba SRL";
        Given(sale, Company());

        var result = await Service().IssueAsync(SaleId, CompanyId);

        // The types that need the expiry of the authorized sequence fail
        // until that date is on record, and the emission says so.
        result.Should().BeOfType<Either<Error, ElectronicInvoiceDto>.Left>();
        var error = ((Either<Error, ElectronicInvoiceDto>.Left)result).Value;
        error.Message.Should().Contain("sequence");
        _dgii.SendRequests.Should().Be(0);

        // The number is linked to the document even though the mapper failed:
        // a number without a document would be a fiscal hole.
        _persisted.Should().NotBeNull();
        _persisted!.ENCF.Should().Be("E310000000001");
        _persisted.Status.Should().Be(EcfStatus.Draft);
        _persisted.RequiresReview.Should().BeTrue();
        _persisted.VerdictNote.Should().Contain("sequence");
        _persisted.SignedXml.Should().BeEmpty();
    }

    [Fact]
    public async Task RecoverAsync_WhenTheAnswerOfTheSendWasLost_AdoptsTheTrackIdTheDgiiGave()
    {
        Given(Sale(), Company());
        GivenDocumentByEncf(EcfStatus.Signed, "E320000000001");

        var result = await Service().RecoverAsync(CompanyId, IssuerRnc, "E320000000001");

        result.Should().BeOfType<Either<Error, ElectronicInvoiceDto>.Right>();
        var dto = ((Either<Error, ElectronicInvoiceDto>.Right)result).Value;
        dto.Status.Should().Be(EcfStatus.Accepted);
        dto.TrackId.Should().Be("track-1");

        // The DGII is asked by the issuer and the e-NCF, and the document is
        // not sent again: sending it twice would burn the number twice.
        _dgii.TrackIdRequests.Should().Be(1);
        _dgii.LookupIssuer.Should().Be(IssuerRnc);
        _dgii.LookupEncf.Should().Be("E320000000001");
        _dgii.SendRequests.Should().Be(0);
    }

    [Fact]
    public async Task RecoverAsync_WhenTheDgiiNeverReceivedTheDocument_LeavesItPending()
    {
        Given(Sale(), Company());
        GivenDocumentByEncf(EcfStatus.Signed, "E320000000001");
        _dgii.LookupTrackId = null;

        var result = await Service().RecoverAsync(CompanyId, IssuerRnc, "E320000000001");

        result.Should().BeOfType<Either<Error, ElectronicInvoiceDto>.Right>();
        var dto = ((Either<Error, ElectronicInvoiceDto>.Right)result).Value;
        dto.Status.Should().Be(EcfStatus.Signed, "the DGII never received it, so the queue is the one that sends it");
        dto.TrackId.Should().BeNull();
        _dgii.SendRequests.Should().Be(0);
        _dgii.StatusRequests.Should().Be(0);
    }

    [Fact]
    public async Task RecoverAsync_WhenTheDocumentAlreadyHasAVerdict_DoesNotAskTheDgii()
    {
        Given(Sale(), Company());
        GivenDocumentByEncf(EcfStatus.Accepted, "E320000000001");

        var result = await Service().RecoverAsync(CompanyId, IssuerRnc, "E320000000001");

        result.Should().BeOfType<Either<Error, ElectronicInvoiceDto>.Right>();
        _dgii.TrackIdRequests.Should().Be(0);
        _dgii.StatusRequests.Should().Be(0);
    }

    [Fact]
    public async Task RecoverAsync_WhenThereIsNoSuchDocument_ReturnsLeft()
    {
        Given(Sale(), Company());

        var result = await Service().RecoverAsync(CompanyId, IssuerRnc, "E320000000999");

        result.Should().BeOfType<Either<Error, ElectronicInvoiceDto>.Left>();
        _dgii.SendRequests.Should().Be(0);
    }

    [Fact]
    public async Task IssueAsync_WithADeactivatedCompany_StillIssuesTheDocument()
    {
        // The fiscal obligation of the client does not disappear with the
        // subscription: a deactivated company still has to be able to issue,
        // because blocking it would turn a collection problem into a tax
        // offence of our client.
        var company = Company();
        company.Deactivate();
        Given(Sale(), company);

        var result = await Service().IssueAsync(SaleId, CompanyId);

        result.Should().BeOfType<Either<Error, ElectronicInvoiceDto>.Right>();
        ((Either<Error, ElectronicInvoiceDto>.Right)result).Value.Status.Should().Be(EcfStatus.Signed);
        _persisted!.Status.Should().Be(EcfStatus.Signed);
    }

    [Fact]
    public async Task IssueAsync_SignsWithTheCertificateOfThatIssuerAndNoOther()
    {
        Given(Sale(), Company());
        var certificate = CreateCertificate();
        _certificates.Setup(c => c.GetSigningCertificateAsync(IssuerRnc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(certificate);

        await Service().IssueAsync(SaleId, CompanyId);

        // The document is signed with the certificate of the RNC that emits
        // it: there is no shared certificate and no fallback.
        var document = _signedXmls.FindIndex(xml => xml.Contains("<eNCF>"));

        document.Should().BeGreaterThanOrEqualTo(0, "the document was signed");
        _signedWith[document].Should().Be(certificate);
    }

    [Fact]
    public async Task IssueAsync_WithAConsumptionInvoiceBelowTheLimit_RecordsTheVerificationUrl()
    {
        Given(Sale(), Company());

        var result = await Service().IssueAsync(SaleId, CompanyId);

        result.Should().BeOfType<Either<Error, ElectronicInvoiceDto>.Right>();
        var dto = ((Either<Error, ElectronicInvoiceDto>.Right)result).Value;

        // A consumption invoice below RD$250,000 travels as a summary, and
        // the QR of its printed representation carries the timbre of that
        // summary.
        var expected = "https://fc.dgii.gov.do/testecf/consultatimbrefc"
                       + $"?rncemisor={IssuerRnc}"
                       + "&encf=E320000000001"
                       + "&montototal=236.00"
                       + "&codigoseguridad=BoXjC8";

        _persisted!.QrUrl.Should().Be(expected);
        dto.QrUrl.Should().Be(expected);
    }

    [Fact]
    public async Task IssueAsync_WithAConsumptionInvoiceAboveTheLimit_RecordsTheTimbreOfTheTaxpayerHost()
    {
        // Above RD$250,000 the invoice itself is the one that travels to the
        // DGII: there is no summary, so the QR points at the timbre of the
        // host of the taxpayer.
        var sale = Sale();
        sale.AddItem(Guid.CreateVersion7(), "Café molido", 2, 200000m, TaxCode.ItbisTasa1);
        Given(sale, Company());

        var result = await Service().IssueAsync(SaleId, CompanyId);

        result.Should().BeOfType<Either<Error, ElectronicInvoiceDto>.Right>();
        _persisted!.Total.Should().Be(472236.00m);
        _persisted.QrUrl.Should().Contain("https://ecf.dgii.gov.do/testecf/consultatimbre");
        _persisted.QrUrl.Should().Contain($"rncemisor={IssuerRnc}");
        _persisted.QrUrl.Should().Contain("encf=E320000000001");
        _persisted.QrUrl.Should().Contain("montototal=472236.00");
        _persisted.QrUrl.Should().Contain("codigoseguridad=BoXjC8");
    }

    [Fact]
    public async Task ProcessPendingAsync_WhenTheDgiiAnswersWithAPathologicalBody_KeepsItCutAndTheDocumentGoesOn()
    {
        Given(Sale(), Company());
        _dgii.AcknowledgementBody = new string('x', 50_000);

        await Service().IssueAsync(SaleId, CompanyId);
        GivenPending(_persisted!);
        await Service().ProcessPendingAsync(CompanyId);

        // The body of the DGII is a diagnostic, not a payload: the row cannot
        // grow without limit, and the cut says so.
        _persisted!.RawResponse!.Length.Should().BeLessThan(50_000);
        _persisted.RawResponse.Should().EndWith("[truncated]");

        // And the cut does not break the emission: the document went to the
        // DGII and waits for its verdict.
        _dgii.SendRequests.Should().Be(1);
        _persisted.Status.Should().Be(EcfStatus.Sent);
        _persisted.TrackId.Should().Be("track-1");
    }

    [Fact]
    public async Task ProcessPendingAsync_WhenTheConversationOfTheTokenFails_DoesNotLeaveItsBodyOnTheDocument()
    {
        Given(Sale(), Company());
        _dgii.TokenFailure = new DgiiException(
            "The DGII rejected the signed seed (BadRequest).",
            """{"token":"eyJhbGciOiJIUzI1NiJ9.dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk","expira":"2026-11-14T11:00:00"}""",
            statusCode: HttpStatusCode.BadRequest);

        await Service().IssueAsync(SaleId, CompanyId);
        GivenPending(_persisted!);
        await Service().ProcessPendingAsync(CompanyId);

        // The body of the conversation of the token can carry the token of
        // the DGII: it is a credential and it does not go on record.
        _persisted!.RawResponse.Should().BeNull();
        _persisted.VerdictNote.Should().NotContain("eyJ");

        // The reason of the failure is still there: only the body is gone.
        _persisted.VerdictNote.Should().Contain("signed seed");
    }

    private void Given(Sale sale, Company company)
    {
        _sales.Setup(r => r.GetSaleByIdAsync(sale.Id, It.IsAny<CancellationToken>())).ReturnsAsync(sale);
        _companies.Setup(r => r.GetCompanyByIdAsync(company.Id, It.IsAny<CancellationToken>())).ReturnsAsync(company);
        _invoices.Setup(r => r.GetBySaleIdAsync(sale.Id, It.IsAny<CancellationToken>())).ReturnsAsync((ElectronicInvoice?)null);
        _sequences.Setup(s => s.NextSequenceAsync(company.Id, It.IsAny<EcfDocumentType>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    private void GivenDocument(EcfStatus status, string encf)
    {
        _invoices.Setup(r => r.GetBySaleIdAsync(SaleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ElectronicInvoice
            {
                Id = Guid.CreateVersion7(),
                CompanyId = CompanyId,
                SaleId = SaleId,
                ENCF = encf,
                DocumentType = EcfDocumentType.Consumer,
                IssuerTaxId = IssuerRnc,
                Status = status
            });
    }

    private void GivenDocumentByEncf(EcfStatus status, string encf)
    {
        _invoices.Setup(r => r.GetByEncfAsync(CompanyId, IssuerRnc, encf, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ElectronicInvoice
            {
                Id = Guid.CreateVersion7(),
                CompanyId = CompanyId,
                SaleId = SaleId,
                ENCF = encf,
                DocumentType = EcfDocumentType.Consumer,
                IssuerTaxId = IssuerRnc,
                Status = status
            });
    }

    // The document the issue leaves behind is the one the queue takes.
    private void GivenPending(ElectronicInvoice invoice)
    {
        _invoices.Setup(i => i.GetPendingForEmissionAsync(
                CompanyId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([invoice]);

        _invoices.Setup(i => i.TryClaimAsync(
                CompanyId, It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _invoices.Setup(i => i.GetByCompanyAndIdAsync(CompanyId, invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);
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
            dgiiOptions: new DgiiOptions { Environment = DgiiEnvironment.PreCertification });
    }

    private static Sale Sale(
        EcfDocumentType documentType = EcfDocumentType.Consumer,
        Guid? companyId = null)
    {
        var sale = new Sale
        {
            Id = SaleId,
            CompanyId = companyId ?? CompanyId,
            Code = "V-0001",
            Status = SaleStatus.Completed,
            DocumentType = documentType,
            CreatedAt = new DateTime(2026, 11, 4)
        };

        sale.AddItem(Guid.CreateVersion7(), "Café molido", 2, 100m, TaxCode.ItbisTasa1);

        return sale;
    }

    private static Company Company() => new()
    {
        Id = CompanyId,
        Name = "Distribuidora del Norte",
        LegalName = "Distribuidora del Norte SRL",
        TaxId = IssuerRnc,
        Address = "Calle Principal 1, Santo Domingo",
        Phone = "809-555-1234",
        Email = "facturacion@distribuidora.do"
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

    // The DGII as the tests see it: it answers the seed, the token, the
    // acknowledgement and the verdict, and it counts what it was asked.
    private sealed class DgiiStub : IDgiiClient
    {
        public DgiiVerdict Verdict { get; set; } = DgiiVerdict.Accepted;

        public string? Refusal { get; set; }

        // The body of the acknowledgement of the send: the DGII answers with
        // more or less of it, and a pathological one has to be cut.
        public string? AcknowledgementBody { get; set; }

        // The failure of the conversation of the token: its body can carry
        // the token of the DGII.
        public Exception? TokenFailure { get; set; }

        public int SendRequests { get; private set; }
        public int StatusRequests { get; private set; }
        public int TrackIdRequests { get; private set; }
        public string? SentXml { get; private set; }
        public string? StatusTrackId { get; private set; }
        public string? LookupTrackId { get; set; } = "track-1";
        public string? LookupIssuer { get; private set; }
        public string? LookupEncf { get; private set; }

        public Task<string> GetSeedAsync(CancellationToken cancellationToken = default)
            => Task.FromResult("<SemillaModel><valor>abc</valor><fecha>2019-03-13T14:33:32-04:00</fecha></SemillaModel>");

        public Task<DgiiToken> RequestTokenAsync(string signedSeedXml, CancellationToken cancellationToken = default)
        {
            if (TokenFailure is not null)
                throw TokenFailure;

            return Task.FromResult(new DgiiToken("jwt", DateTimeOffset.UtcNow.AddHours(1), DateTimeOffset.UtcNow));
        }

        public Task<DgiiReceipt> SendElectronicInvoiceAsync(string signedEcfXml, string token, CancellationToken cancellationToken = default)
        {
            SendRequests++;
            SentXml = signedEcfXml;

            return Task.FromResult(Refusal is null
                ? new DgiiReceipt("track-1", null, null) { RawBody = AcknowledgementBody }
                : new DgiiReceipt(null, Refusal, null));
        }

        public Task<DgiiStatus> GetStatusAsync(string trackId, string token, CancellationToken cancellationToken = default)
        {
            StatusRequests++;
            StatusTrackId = trackId;

            return Task.FromResult(new DgiiStatus(
                trackId,
                Verdict,
                Verdict.ToString(),
                "10100000101",
                "E320000000001",
                true,
                DateTimeOffset.UtcNow,
                []));
        }

        public Task<DgiiTrackLookup> GetTrackIdAsync(string issuerTaxId, string encf, string token, CancellationToken cancellationToken = default)
        {
            TrackIdRequests++;
            LookupIssuer = issuerTaxId;
            LookupEncf = encf;

            return Task.FromResult(new DgiiTrackLookup(LookupTrackId, "Aceptado", DateTimeOffset.UtcNow));
        }
    }
}

// The QR of a document: the verification URL drawn as an image. The SVG is
// the one the printed representation carries, because it scales without losing
// lines; the PNG is the one a screen shows.
public class ElectronicInvoiceQrCodeTests
{
    private const string VerificationUrl =
        "https://fc.dgii.gov.do/ecf/consultatimbrefc?rncemisor=131880738&encf=E320000000064&montototal=6225.09&codigoseguridad=BoXjC8";

    private const string ShortUrl =
        "https://fc.dgii.gov.do/ecf/consultatimbrefc?encf=E320000000064";

    // The URL of the timbre of a document that is not a summary, with every
    // parameter the DGII lists (the example of the specification, p. 41).
    private const string LongTimbreUrl =
        "https://ecf.dgii.gov.do/testecf/consultatimbre?rncemisor=130000001&rnccomprador=130000002&encf=e310000000001&fechaemision=10-10-2020&montototal=02.11&fechafirma=10-10-2020%2009:00:00&codigoseguridad=dcp79q";

    [Fact]
    public void Svg_DrawsTheVerificationUrlAsAnImage()
    {
        var svg = QrCodeRenderer.Svg(VerificationUrl);

        var document = XDocument.Parse(svg);

        document.Root!.Name.LocalName.Should().Be("svg");
    }

    [Fact]
    public void Png_DrawsTheVerificationUrlAsAnImage()
    {
        var png = QrCodeRenderer.Png(VerificationUrl);

        png.Take(8).Should().Equal(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A);
    }

    [Fact]
    public void Svg_UsesTheVersionTheDgiiAsksFor()
    {
        // The version 8 of the symbol is seventeen modules plus four per
        // version step: forty-nine. The quiet zone adds four more on each
        // side, so the drawing measures fifty-seven.
        var svg = XDocument.Parse(QrCodeRenderer.Svg(ShortUrl));

        svg.Root!.Attribute("viewBox")!.Value.Should().Be("0 0 57 57");
    }

    [Fact]
    public void Png_UsesTheVersionTheDgiiAsksFor()
    {
        // Fifty-seven modules at twenty pixels each.
        var png = QrCodeRenderer.Png(ShortUrl);

        Width(png).Should().Be(1140);
    }

    [Fact]
    public void Svg_EncodesTheTimbreUrlEvenWhenItDoesNotFitTheVersion()
    {
        // The URL of the timbre of a document that is not a summary is longer
        // than what the version 8 holds at the level that suits a URL: the
        // symbol grows instead of failing.
        var svg = XDocument.Parse(QrCodeRenderer.Svg(LongTimbreUrl));

        svg.Root!.Attribute("viewBox")!.Value.Should().NotBe("0 0 57 57");
    }

    [Fact]
    public void Png_EncodesTheTimbreUrlEvenWhenItDoesNotFitTheVersion()
    {
        var png = QrCodeRenderer.Png(LongTimbreUrl);

        Width(png).Should().BeGreaterThan(1140);
    }

    [Fact]
    public void Svg_UsesTheVersionTheDgiiAsksForInTheTimbreOfTheSummary()
    {
        // The timbre of the summary fits the version 8 at the level that
        // corrects a URL (152 bytes), so the printed symbol is the version
        // the DGII names.
        var svg = XDocument.Parse(QrCodeRenderer.Svg(VerificationUrl));

        svg.Root!.Attribute("viewBox")!.Value.Should().Be("0 0 57 57");
    }

    [Fact]
    public void Png_UsesTheVersionTheDgiiAsksForInTheTimbreOfTheSummary()
    {
        var png = QrCodeRenderer.Png(VerificationUrl);

        Width(png).Should().Be(1140);
    }

    private static int Width(byte[] png)
        => (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19];
}

// The QR of a document travels with the API: the frontend shows it on the
// printed representation, and the URL it carries is the one the receiver opens
// to check the document.
[Collection("VolterpApiFactory")]
public class ElectronicInvoiceQrEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    private static readonly object SeedLock = new();
    private static bool _seeded;
    private static Guid _invoiceId;
    private static Guid _invoiceWithoutUrlId;

    public ElectronicInvoiceQrEndpointTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _factory.EnsureSchemaCreated();

        lock (SeedLock)
        {
            if (!_seeded)
            {
                SeedAsync().GetAwaiter().GetResult();
                _seeded = true;
            }
        }
    }

    [Fact]
    public async Task TheQrOfTheDocumentIsServedAsAnImage()
    {
        SetAuth(Guid7TestValues.CompanyA);

        var response = await _client.GetAsync($"/api/ElectronicInvoices/{_invoiceId}/qr.svg");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/svg+xml");

        var body = await response.Content.ReadAsStringAsync();

        XDocument.Parse(body).Root!.Name.LocalName.Should().Be("svg");
    }

    [Fact]
    public async Task TheQrOfTheDocumentOfAnotherTenantIsNotServed()
    {
        SetAuth(Guid7TestValues.CompanyB);

        var response = await _client.GetAsync($"/api/ElectronicInvoices/{_invoiceId}/qr.svg");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TheQrOfADocumentWithoutAVerificationUrlIsNotServed()
    {
        SetAuth(Guid7TestValues.CompanyA);

        var response = await _client.GetAsync($"/api/ElectronicInvoices/{_invoiceWithoutUrlId}/qr.svg");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TheQrOfTheDocumentIsServedAsAnImageOfPng()
    {
        SetAuth(Guid7TestValues.CompanyA);

        var response = await _client.GetAsync($"/api/ElectronicInvoices/{_invoiceId}/qr.png");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/png");

        var body = await response.Content.ReadAsByteArrayAsync();

        body.Take(4).Should().Equal(0x89, 0x50, 0x4E, 0x47);
    }

    [Fact]
    public void TheApiCanBuildTheEmitterOfTheDocuments()
    {
        // The emitter carries the environment of the DGII, and the container
        // has to be able to give it.
        using var scope = _factory.Services.CreateScope();

        var service = scope.ServiceProvider.GetRequiredService<IElectronicInvoiceService>();

        service.Should().NotBeNull();
    }

    private void SetAuth(Guid companyId)
    {
        var token = JwtTestHelper.MintToken(_factory, companyId, Guid7TestValues.UserA);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VolterpDbContext>();

        db.Companies.Add(new Company
        {
            Id = Guid7TestValues.CompanyA,
            Name = "Empresa",
            TaxId = "101500018",
            Address = "Dirección",
            LegalName = "Empresa SRL",
            Phone = "8090000000",
            Email = "empresa@test.com"
        });

        var saleId = Guid.CreateVersion7();
        var otherSaleId = Guid.CreateVersion7();

        db.Sales.AddRange(
            new Sale
            {
                Id = saleId,
                CompanyId = Guid7TestValues.CompanyA,
                Code = "V-0001",
                Status = SaleStatus.Completed,
                DocumentType = EcfDocumentType.Consumer,
                Subtotal = 200m,
                Itbis = 36m,
                Total = 236m
            },
            new Sale
            {
                Id = otherSaleId,
                CompanyId = Guid7TestValues.CompanyA,
                Code = "V-0002",
                Status = SaleStatus.Completed,
                DocumentType = EcfDocumentType.Consumer,
                Subtotal = 200m,
                Itbis = 36m,
                Total = 236m
            });

        _invoiceId = Guid.CreateVersion7();
        _invoiceWithoutUrlId = Guid.CreateVersion7();

        db.ElectronicInvoices.AddRange(
            new ElectronicInvoice
            {
                Id = _invoiceId,
                CompanyId = Guid7TestValues.CompanyA,
                SaleId = saleId,
                ENCF = "E320000000001",
                DocumentType = EcfDocumentType.Consumer,
                IssuerTaxId = "101500018",
                Subtotal = 200m,
                Itbis = 36m,
                Total = 236m,
                SignedXml = "<ECF />",
                SecurityCode = "BoXjC8",
                QrUrl = "https://fc.dgii.gov.do/testecf/consultatimbrefc?rncemisor=101500018&encf=E320000000001&montototal=236.00&codigoseguridad=BoXjC8",
                Status = EcfStatus.Accepted
            },
            new ElectronicInvoice
            {
                Id = _invoiceWithoutUrlId,
                CompanyId = Guid7TestValues.CompanyA,
                SaleId = otherSaleId,
                ENCF = "E320000000002",
                DocumentType = EcfDocumentType.Consumer,
                IssuerTaxId = "101500018",
                Subtotal = 200m,
                Itbis = 36m,
                Total = 236m,
                SignedXml = "<ECF />",
                SecurityCode = "BoXjC8",
                Status = EcfStatus.Accepted
            });

        await db.SaveChangesAsync();
    }
}
