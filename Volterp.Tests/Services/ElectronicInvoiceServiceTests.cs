using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using EitherWay;
using FluentAssertions;
using Moq;
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
using Xunit;

namespace Volterp.Tests.Services;

// The orchestrator of the emission: a completed sale becomes a signed
// document, sent to the DGII, with the verdict on record.
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
    public async Task IssueAsync_WithACompletedSale_IssuesTheDocumentAndLandsTheVerdict()
    {
        Given(Sale(), Company());

        var result = await Service().IssueAsync(SaleId, CompanyId);

        result.Should().BeOfType<Either<Error, ElectronicInvoiceDto>.Right>();
        var dto = ((Either<Error, ElectronicInvoiceDto>.Right)result).Value;
        dto.ENCF.Should().Be("E320000000001");
        dto.Status.Should().Be(EcfStatus.Accepted);

        // The number comes from the sequence of the issuer, and it is the only
        // one that is taken.
        _sequences.Verify(s => s.NextSequenceAsync(CompanyId, EcfDocumentType.Consumer, It.IsAny<CancellationToken>()), Times.Once);

        // The document that was signed carries the number the DGII will read.
        _signedXmls.Should().Contain(xml => xml.Contains("<eNCF>E320000000001</eNCF>"));

        // And the DGII received the signed document, not the unsigned one.
        _dgii.SendRequests.Should().Be(1);
        _dgii.SentXml.Should().Be(SignedXml);
        _dgii.StatusTrackId.Should().Be("track-1");

        // The document is on record with its track id and its verdict.
        _persisted.Should().NotBeNull();
        _persisted!.CompanyId.Should().Be(CompanyId);
        _persisted.SaleId.Should().Be(SaleId);
        _persisted.ENCF.Should().Be("E320000000001");
        _persisted.DocumentType.Should().Be(EcfDocumentType.Consumer);
        _persisted.IssuerTaxId.Should().Be(IssuerRnc);
        _persisted.SignedXml.Should().Be(SignedXml);
        _persisted.TrackId.Should().Be("track-1");
        _persisted.Status.Should().Be(EcfStatus.Accepted);

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
    public async Task IssueAsync_WithoutAnActiveCertificateOfTheIssuer_ReturnsLeft()
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
        // it the emission fails closed: no document and no trip to the DGII.
        _certificates.Verify(c => c.GetSigningCertificateAsync(IssuerRnc, It.IsAny<CancellationToken>()), Times.Once);
        _dgii.SendRequests.Should().Be(0);
        _persisted.Should().BeNull();
    }

    [Fact]
    public async Task IssueAsync_WhenTheDgiiRefusesTheDocument_KeepsTheDocumentOutOfSent()
    {
        Given(Sale(), Company());
        _dgii.Refusal = "El archivo no cumple con el formato";

        var result = await Service().IssueAsync(SaleId, CompanyId);

        result.Should().BeOfType<Either<Error, ElectronicInvoiceDto>.Left>();
        var error = ((Either<Error, ElectronicInvoiceDto>.Left)result).Value;
        error.Message.Should().Contain("El archivo no cumple con el formato");

        // The document is on record as signed, and it does not claim to be
        // sent: it never was.
        _persisted.Should().NotBeNull();
        _persisted!.Status.Should().Be(EcfStatus.Signed);
        _persisted.TrackId.Should().BeNull();
        _dgii.StatusRequests.Should().Be(0);
    }

    [Fact]
    public async Task IssueAsync_WhenTheVerdictIsInProcess_KeepsTheDocumentInProcess()
    {
        Given(Sale(), Company());
        _dgii.Verdict = DgiiVerdict.InProcess;

        var result = await Service().IssueAsync(SaleId, CompanyId);

        result.Should().BeOfType<Either<Error, ElectronicInvoiceDto>.Right>();
        var dto = ((Either<Error, ElectronicInvoiceDto>.Right)result).Value;
        dto.Status.Should().Be(EcfStatus.InProcess);

        _persisted.Should().NotBeNull();
        _persisted!.Status.Should().Be(EcfStatus.InProcess,
            "the DGII is still validating the document, so it is not accepted yet");
        _persisted.TrackId.Should().Be("track-1");
    }

    [Fact]
    public async Task IssueAsync_WhenTheTypeNeedsTheExpiryOfTheSequence_ReturnsLeft()
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
        _persisted.Should().BeNull();
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
        ((Either<Error, ElectronicInvoiceDto>.Right)result).Value.Status.Should().Be(EcfStatus.Accepted);
        _dgii.SendRequests.Should().Be(1);
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

    private ElectronicInvoiceService Service()
    {
        var authenticator = new DgiiAuthenticator(_dgii, _signer.Object, new DgiiTokenCache());

        return new ElectronicInvoiceService(
            _unitOfWork.Object,
            new EcfXmlWriter(),
            _signer.Object,
            _certificates.Object,
            authenticator,
            _dgii);
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
            => Task.FromResult(new DgiiToken("jwt", DateTimeOffset.UtcNow.AddHours(1), DateTimeOffset.UtcNow));

        public Task<DgiiReceipt> SendElectronicInvoiceAsync(string signedEcfXml, string token, CancellationToken cancellationToken = default)
        {
            SendRequests++;
            SentXml = signedEcfXml;

            return Task.FromResult(Refusal is null
                ? new DgiiReceipt("track-1", null, null)
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
