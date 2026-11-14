using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using EitherWay;
using FluentAssertions;
using Moq;
using Volterp.Application.DTOs.SaleDtos;
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

// The sale that is completed queues its e-CF: the sale is a commercial fact
// and the fiscal document travels in the background, in the durable queue.
public class SaleElectronicInvoiceTests
{
    // The signer of the tests answers with a document that carries a
    // signature, because the security code is taken out of it.
    private const string SignedXml = "<ECF Signed=\"true\"><Signature xmlns=\"http://www.w3.org/2000/09/xmldsig#\"><SignatureValue>BoXjC8ZmFrQnVuZXZlck9yaWdpbmFs</SignatureValue></Signature></ECF>";
    private const string IssuerRnc = "10100000101";

    private static readonly Guid CompanyId = Guid.Parse("0192b9c7-b6a3-7def-8c5e-1f2a3b4c5d6e");
    private static readonly Guid SaleId = Guid.Parse("0192b9c7-b6a3-7def-8c5e-1f2a3b4c5d74");

    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ISaleRepository> _sales = new();
    private readonly Mock<IProductRepository> _products = new();
    private readonly Mock<ICompanyRepository> _companies = new();
    private readonly Mock<IElectronicInvoiceRepository> _invoices = new();
    private readonly Mock<IElectronicInvoiceSequenceRepository> _sequences = new();
    private readonly Mock<IEcfSigner> _signer = new();
    private readonly Mock<ISigningCertificateProvider> _certificates = new();
    private readonly DgiiStub _dgii = new();

    private Sale _sale = null!;
    private ElectronicInvoice? _persisted;

    public SaleElectronicInvoiceTests()
    {
        _unitOfWork.Setup(u => u.Sales).Returns(_sales.Object);
        _unitOfWork.Setup(u => u.Products).Returns(_products.Object);
        _unitOfWork.Setup(u => u.Companies).Returns(_companies.Object);
        _unitOfWork.Setup(u => u.ElectronicInvoices).Returns(_invoices.Object);
        _unitOfWork.Setup(u => u.ElectronicInvoiceSequences).Returns(_sequences.Object);
        _unitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _signer.Setup(s => s.Sign(It.IsAny<string>(), It.IsAny<X509Certificate2>())).Returns(SignedXml);

        _invoices.Setup(i => i.AddAsync(It.IsAny<ElectronicInvoice>(), It.IsAny<CancellationToken>()))
            .Callback((ElectronicInvoice invoice, CancellationToken _) => _persisted = invoice)
            .Returns(Task.CompletedTask);

        _certificates.Setup(c => c.GetSigningCertificateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCertificate());
    }

    [Fact]
    public async Task CompleteSaleAsync_LeavesTheDocumentPendingInTheQueue()
    {
        Given();

        var result = await Service().CompleteSaleAsync(SaleId, CompanyId, null);

        result.Should().BeOfType<Either<Error, SaleDto>.Right>();

        // The sale is completed: that is the commercial fact.
        _sale.Status.Should().Be(SaleStatus.Completed);

        // The document is signed and waiting for the queue to send it.
        _persisted.Should().NotBeNull();
        _persisted!.Status.Should().Be(EcfStatus.Signed);
        _persisted.TrackId.Should().BeNull();
        _persisted.SaleId.Should().Be(SaleId);
        _persisted.RequiresReview.Should().BeFalse();

        // The queue is the one that talks to the DGII.
        _dgii.SendRequests.Should().Be(0);
    }

    [Fact]
    public async Task UpdateSaleAsync_WithCompletedStatus_ReturnsLeftAndLeavesTheSalePending()
    {
        Given();
        var request = new UpdateSaleRequest(
            null,
            "Cliente",
            SaleStatus.Completed,
            200m,
            null,
            [new CreateSaleItemRequest(Guid.CreateVersion7(), "Café molido", null, null, null, 2, 100m, 200m)]);

        var result = await Service().UpdateSaleAsync(SaleId, CompanyId, request, null);

        result.Should().BeOfType<Either<Error, SaleDto>.Left>();
        var error = ((Either<Error, SaleDto>.Left)result).Value;
        error.Message.Should().Contain("completion operation");

        _sale.Status.Should().Be(SaleStatus.Pending);
        _sales.Verify(r => r.UpdateSaleAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateSaleAsync_WhenTheSaleIsAlreadyCompleted_ReturnsLeftAndLeavesTheSaleUnchanged()
    {
        Given();
        _sale.Status = SaleStatus.Completed;
        _sale.CalculateTotals();
        var originalTotal = _sale.Total;
        var request = new UpdateSaleRequest(
            null,
            "Cliente",
            SaleStatus.Pending,
            200m,
            null,
            [new CreateSaleItemRequest(Guid.CreateVersion7(), "Café molido", null, null, null, 2, 100m, 200m)]);

        var result = await Service().UpdateSaleAsync(SaleId, CompanyId, request, null);

        result.Should().BeOfType<Either<Error, SaleDto>.Left>();
        var error = ((Either<Error, SaleDto>.Left)result).Value;
        error.Message.Should().Be("A completed sale cannot be updated.");

        _sale.Status.Should().Be(SaleStatus.Completed);
        _sale.Total.Should().Be(originalTotal, "the fiscal document of the sale must keep matching it");
        _sales.Verify(r => r.UpdateSaleAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CompleteSaleAsync_WhenTheSignatureFails_LeavesTheSaleCompletedAndTheDocumentForReview()
    {
        Given();
        _signer.Setup(s => s.Sign(It.IsAny<string>(), It.IsAny<X509Certificate2>()))
            .Throws(new InvalidOperationException("The certificate of the issuer does not open."));

        var result = await Service().CompleteSaleAsync(SaleId, CompanyId, null);

        // The sale is completed even though the document could not be signed:
        // the sale is a commercial fact and the fiscal obligation is the
        // client's, so it cannot be undone by it.
        result.Should().BeOfType<Either<Error, SaleDto>.Right>();
        _sale.Status.Should().Be(SaleStatus.Completed);

        // The failure is not lost: the document stays on record for a human.
        _persisted.Should().NotBeNull();
        _persisted!.Status.Should().Be(EcfStatus.Draft);
        _persisted.RequiresReview.Should().BeTrue();
        _persisted.VerdictNote.Should().Contain("does not open");
        _persisted.SignedXml.Should().BeEmpty();
    }

    private void Given()
    {
        _sale = new Sale
        {
            Id = SaleId,
            CompanyId = CompanyId,
            Code = "V-0001",
            Status = SaleStatus.Pending,
            DocumentType = EcfDocumentType.Consumer,
            CreatedAt = new DateTime(2026, 11, 4)
        };

        _sale.AddItem(Guid.CreateVersion7(), "Café molido", 2, 100m, TaxCode.ItbisTasa1);

        _sales.Setup(r => r.GetSaleByIdAsync(SaleId, It.IsAny<CancellationToken>())).ReturnsAsync(_sale);
        _sales.Setup(r => r.UpdateSaleAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _products.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _companies.Setup(r => r.GetCompanyByIdAsync(CompanyId, It.IsAny<CancellationToken>())).ReturnsAsync(Company());
        _invoices.Setup(r => r.GetBySaleIdAsync(SaleId, It.IsAny<CancellationToken>())).ReturnsAsync((ElectronicInvoice?)null);
        _sequences.Setup(r => r.NextSequenceAsync(CompanyId, It.IsAny<EcfDocumentType>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    private SaleService Service()
    {
        var authenticator = new DgiiAuthenticator(_dgii, _signer.Object, new DgiiTokenCache());

        var invoices = new ElectronicInvoiceService(
            _unitOfWork.Object,
            new EcfXmlWriter(),
            _signer.Object,
            _certificates.Object,
            authenticator,
            _dgii,
            dgiiOptions: new DgiiOptions { Environment = DgiiEnvironment.PreCertification });

        return new SaleService(_unitOfWork.Object, invoices);
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

    // The DGII as the tests see it: the issue does not talk to it, so the
    // stub only counts what it was asked.
    private sealed class DgiiStub : IDgiiClient
    {
        public int SendRequests { get; private set; }

        public Task<string> GetSeedAsync(CancellationToken cancellationToken = default)
            => Task.FromResult("<SemillaModel><valor>abc</valor><fecha>2019-03-13T14:33:32-04:00</fecha></SemillaModel>");

        public Task<DgiiToken> RequestTokenAsync(string signedSeedXml, CancellationToken cancellationToken = default)
            => Task.FromResult(new DgiiToken("jwt", DateTimeOffset.UtcNow.AddHours(1), DateTimeOffset.UtcNow));

        public Task<DgiiReceipt> SendElectronicInvoiceAsync(string signedEcfXml, string token, CancellationToken cancellationToken = default)
        {
            SendRequests++;
            return Task.FromResult(new DgiiReceipt("track-1", null, null));
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
            => Task.FromResult(new DgiiTrackLookup("track-1", "Aceptado", DateTimeOffset.UtcNow));
    }
}
