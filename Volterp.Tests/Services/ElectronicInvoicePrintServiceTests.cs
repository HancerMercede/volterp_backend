using EitherWay;
using FluentAssertions;
using Moq;
using Volterp.Application.DTOs.ElectronicInvoicePrintDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Interfaces;
using Volterp.Application.Services;
using Volterp.Domain.Entities;
using Volterp.ElectronicInvoicing;
using Xunit;

namespace Volterp.Tests.Services;

// The printed representation of an e-CF. The service answers with the
// representation of the document that was signed, and it fails closed: a
// document that cannot be printed says why.
public class ElectronicInvoicePrintServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IElectronicInvoiceRepository> _invoices = new();

    private readonly Guid _companyId = Guid.CreateVersion7();
    private readonly Guid _invoiceId = Guid.CreateVersion7();

    public ElectronicInvoicePrintServiceTests()
    {
        _unitOfWork.Setup(u => u.ElectronicInvoices).Returns(_invoices.Object);
    }

    [Fact]
    public async Task PrintAsync_WithoutAnId_ReturnsLeft()
    {
        var result = await Service().PrintAsync(Guid.Empty, _companyId);

        result.Should().BeOfType<Either<Error, ElectronicInvoicePrintDto>.Left>();
        ((Either<Error, ElectronicInvoicePrintDto>.Left)result).Value.Message
            .Should().Be("The id of the e-CF cannot be empty.");
    }

    [Fact]
    public async Task PrintAsync_WithoutACompany_ReturnsLeft()
    {
        var result = await Service().PrintAsync(_invoiceId, Guid.Empty);

        result.Should().BeOfType<Either<Error, ElectronicInvoicePrintDto>.Left>();
        ((Either<Error, ElectronicInvoicePrintDto>.Left)result).Value.Message
            .Should().Be("The company cannot be empty.");
    }

    [Fact]
    public async Task PrintAsync_WhenTheDocumentDoesNotExist_ReturnsLeft()
    {
        Given(null);

        var result = await Service().PrintAsync(_invoiceId, _companyId);

        result.Should().BeOfType<Either<Error, ElectronicInvoicePrintDto>.Left>();
        ((Either<Error, ElectronicInvoicePrintDto>.Left)result).Value.Message
            .Should().Be("The e-CF does not exist.");
    }

    [Fact]
    public async Task PrintAsync_WhenTheDocumentIsOfAnotherCompany_ReturnsLeft()
    {
        Given(Invoice(EcfStatus.Accepted, QrUrl: "https://ecf.dgii.gov.do/testecf/consultatimbre"));

        var result = await Service().PrintAsync(_invoiceId, Guid.CreateVersion7());

        result.Should().BeOfType<Either<Error, ElectronicInvoicePrintDto>.Left>();
        ((Either<Error, ElectronicInvoicePrintDto>.Left)result).Value.Message
            .Should().Be("The e-CF does not exist.");
    }

    [Fact]
    public async Task PrintAsync_WhenTheDocumentIsNotSigned_ReturnsLeft()
    {
        Given(Invoice(EcfStatus.Draft, QrUrl: null, signedXml: null));

        var result = await Service().PrintAsync(_invoiceId, _companyId);

        result.Should().BeOfType<Either<Error, ElectronicInvoicePrintDto>.Left>();
        ((Either<Error, ElectronicInvoicePrintDto>.Left)result).Value.Message
            .Should().Be("The e-CF is not signed.");
    }

    [Fact]
    public async Task PrintAsync_WhenTheDocumentWasRejected_ReturnsLeft()
    {
        Given(Invoice(EcfStatus.Rejected, QrUrl: "https://ecf.dgii.gov.do/testecf/consultatimbre"));

        var result = await Service().PrintAsync(_invoiceId, _companyId);

        result.Should().BeOfType<Either<Error, ElectronicInvoicePrintDto>.Left>();
        ((Either<Error, ElectronicInvoicePrintDto>.Left)result).Value.Message.Should().Contain("rejected");
    }

    [Fact]
    public async Task PrintAsync_WhenTheDocumentWasAnnulled_ReturnsLeft()
    {
        Given(Invoice(EcfStatus.Annulled, QrUrl: "https://ecf.dgii.gov.do/testecf/consultatimbre"));

        var result = await Service().PrintAsync(_invoiceId, _companyId);

        result.Should().BeOfType<Either<Error, ElectronicInvoicePrintDto>.Left>();
        ((Either<Error, ElectronicInvoicePrintDto>.Left)result).Value.Message.Should().Contain("annulled");
    }

    [Fact]
    public async Task PrintAsync_WhenTheDocumentIsNotPrintable_ReturnsLeft()
    {
        Given(Invoice(EcfStatus.InProcess, QrUrl: "https://ecf.dgii.gov.do/testecf/consultatimbre"));

        var result = await Service().PrintAsync(_invoiceId, _companyId);

        result.Should().BeOfType<Either<Error, ElectronicInvoicePrintDto>.Left>();
        ((Either<Error, ElectronicInvoicePrintDto>.Left)result).Value.Message.Should().Contain("printable");
    }

    [Fact]
    public async Task PrintAsync_WhenTheDocumentHasNoVerificationUrl_ReturnsLeft()
    {
        Given(Invoice(EcfStatus.Accepted, QrUrl: null));

        var result = await Service().PrintAsync(_invoiceId, _companyId);

        result.Should().BeOfType<Either<Error, ElectronicInvoicePrintDto>.Left>();
        ((Either<Error, ElectronicInvoicePrintDto>.Left)result).Value.Message
            .Should().Be("The e-CF has no verification URL.");
    }

    private ElectronicInvoicePrintService Service() => new(_unitOfWork.Object);

    private void Given(ElectronicInvoice? invoice)
    {
        _invoices.Setup(i => i.GetByIdAsync(_invoiceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);
    }

    private ElectronicInvoice Invoice(
        EcfStatus status, string? QrUrl, string? signedXml = "<ECF />")
        => new()
        {
            Id = _invoiceId,
            CompanyId = _companyId,
            SaleId = Guid.CreateVersion7(),
            ENCF = "E310000000001",
            DocumentType = EcfDocumentType.FiscalCredit,
            IssuerTaxId = "130862346",
            SignedXml = signedXml!,
            QrUrl = QrUrl!,
            Status = status
        };
}
