using EitherWay;
using FluentAssertions;
using Moq;
using Volterp.Application.DTOs.NormalFiscalDocumentDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Interfaces;
using Volterp.Application.Services;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.ElectronicInvoicing;
using Xunit;

namespace Volterp.Tests.Services;

// The paper document of a sale: the number comes from the authorized range of
// the normal documents of the issuer, the record carries the fiscal data of
// the sale, and a failure never leaves a number consumed without a document.
public class NormalFiscalDocumentServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ISaleRepository> _sales = new();
    private readonly Mock<INormalFiscalDocumentRepository> _documents = new();
    private readonly Mock<INormalFiscalSequenceRepository> _sequences = new();
    private readonly Mock<IElectronicInvoiceSequenceRepository> _electronicSequences = new();
    private readonly Mock<ICompanyRepository> _companies = new();

    private readonly Guid _companyId = Guid.CreateVersion7();
    private readonly Guid _otherCompanyId = Guid.CreateVersion7();

    public NormalFiscalDocumentServiceTests()
    {
        _unitOfWork.Setup(u => u.Sales).Returns(_sales.Object);
        _unitOfWork.Setup(u => u.NormalFiscalDocuments).Returns(_documents.Object);
        _unitOfWork.Setup(u => u.NormalFiscalSequences).Returns(_sequences.Object);
        _unitOfWork.Setup(u => u.ElectronicInvoiceSequences).Returns(_electronicSequences.Object);
        _unitOfWork.Setup(u => u.Companies).Returns(_companies.Object);

        // The paper document is issued by the RNC the sale names; without
        // one, the company issues with its own.
        var issuers = new Mock<ICompanyIssuerRepository>();
        issuers.Setup(i => i.ResolveIssuerTaxIdAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("10100000101");
        _unitOfWork.Setup(u => u.CompanyIssuers).Returns(issuers.Object);
        _unitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _companies.Setup(c => c.GetCompanyByIdAsync(_companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Company { Id = _companyId, TaxId = "10100000101" });
    }

    [Fact]
    public async Task IssueAsync_RecordsTheDocumentWithTheNumberTakenFromTheRange()
    {
        GivenTheSequence(42);
        var sale = GivenTheSale();

        var result = await Service().IssueAsync(sale.Id, _companyId);

        result.Should().BeOfType<Either<Error, NormalFiscalDocumentDto>.Right>();
        var dto = ((Either<Error, NormalFiscalDocumentDto>.Right)result).Value;

        dto.Number.Should().Be("B0200000042");
        dto.Number.Should().HaveLength(11);
        dto.DocumentType.Should().Be(NormalFiscalDocumentType.Consumer);
        dto.CompanyId.Should().Be(_companyId);
        dto.SaleId.Should().Be(sale.Id);
        dto.IssuerTaxId.Should().Be("10100000101");
        dto.ReceiverTaxId.Should().Be("101010101");
        dto.ReceiverName.Should().Be("Cliente de Prueba SRL");
        dto.Subtotal.Should().Be(200m);
        dto.Itbis.Should().Be(36m);
        dto.Total.Should().Be(236m);
        dto.IssuedAt.Should().NotBe(default);

        _documents.Verify(d => d.AddAsync(
            It.Is<NormalFiscalDocument>(document => document.Number == "B0200000042"),
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IssueAsync_ForACreditFiscalSale_UsesTheNormalCreditFiscalNumber()
    {
        GivenTheSequence(1);
        var sale = GivenTheSale(EcfDocumentType.FiscalCredit);

        var result = await Service().IssueAsync(sale.Id, _companyId);

        var dto = ((Either<Error, NormalFiscalDocumentDto>.Right)result).Value;
        dto.Number.Should().Be("B0100000001");
        dto.DocumentType.Should().Be(NormalFiscalDocumentType.FiscalCredit);
    }

    [Fact]
    public async Task IssueAsync_WhenTheTypeHasNoNormalCounterpart_ReturnsLeft()
    {
        GivenTheSequence(1);
        var sale = GivenTheSale(EcfDocumentType.Purchase);

        var result = await Service().IssueAsync(sale.Id, _companyId);

        result.Should().BeOfType<Either<Error, NormalFiscalDocumentDto>.Left>();
        ((Either<Error, NormalFiscalDocumentDto>.Left)result).Value.Message.Should().Contain("no normal fiscal document");
        _documents.Verify(d => d.AddAsync(It.IsAny<NormalFiscalDocument>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IssueAsync_ForANoteWithoutTheDocumentItModifies_ReturnsLeft()
    {
        GivenTheSequence(1);
        var sale = GivenTheSale(EcfDocumentType.CreditNote);

        var result = await Service().IssueAsync(sale.Id, _companyId);

        result.Should().BeOfType<Either<Error, NormalFiscalDocumentDto>.Left>();
        ((Either<Error, NormalFiscalDocumentDto>.Left)result).Value.Message.Should().Contain("modifies");
        _documents.Verify(d => d.AddAsync(It.IsAny<NormalFiscalDocument>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IssueAsync_ForASaleOfAnotherCompany_ReturnsLeft()
    {
        GivenTheSequence(1);
        var sale = GivenTheSale();

        var result = await Service().IssueAsync(sale.Id, _otherCompanyId);

        result.Should().BeOfType<Either<Error, NormalFiscalDocumentDto>.Left>();
        ((Either<Error, NormalFiscalDocumentDto>.Left)result).Value.Message.Should().Contain("another company");
        _sequences.Verify(s => s.NextSequenceAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<NormalFiscalDocumentType>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IssueAsync_NeverTakesANumberOfTheElectronicRange()
    {
        GivenTheSequence(1);
        var sale = GivenTheSale();

        await Service().IssueAsync(sale.Id, _companyId);

        _electronicSequences.Verify(s => s.NextSequenceAsync(
            It.IsAny<Guid>(), It.IsAny<EcfDocumentType>(), It.IsAny<CancellationToken>()), Times.Never,
            "the normal document has a range of its own");
    }

    [Fact]
    public async Task IssueAsync_WhenTheSaleAlreadyHasADocument_KeepsItAndTakesNoNumber()
    {
        var sale = GivenTheSale();
        var existing = new NormalFiscalDocument
        {
            Id = Guid.CreateVersion7(),
            CompanyId = _companyId,
            SaleId = sale.Id,
            Number = "B0200000007",
            DocumentType = NormalFiscalDocumentType.Consumer,
            IssuerTaxId = "10100000101"
        };

        _documents.Setup(d => d.GetBySaleIdAsync(_companyId, sale.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await Service().IssueAsync(sale.Id, _companyId);

        var dto = ((Either<Error, NormalFiscalDocumentDto>.Right)result).Value;
        dto.Id.Should().Be(existing.Id);
        dto.Number.Should().Be("B0200000007");
        _sequences.Verify(s => s.NextSequenceAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<NormalFiscalDocumentType>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IssueAsync_WhenTheNumberCannotBeTaken_ReturnsLeftAndRollsBack()
    {
        var sale = GivenTheSale();

        _sequences.Setup(s => s.NextSequenceAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<NormalFiscalDocumentType>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(
                "The authorized range of normal documents for the type 2 is exhausted."));

        var result = await Service().IssueAsync(sale.Id, _companyId);

        result.Should().BeOfType<Either<Error, NormalFiscalDocumentDto>.Left>();
        ((Either<Error, NormalFiscalDocumentDto>.Left)result).Value.Message.Should().Contain("exhausted");
        _documents.Verify(d => d.AddAsync(It.IsAny<NormalFiscalDocument>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IssueAsync_WhenTheDocumentCannotBePersisted_ReturnsLeftAndRollsBack()
    {
        GivenTheSequence(1);
        var sale = GivenTheSale();

        _unitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("The document could not be persisted."));

        var result = await Service().IssueAsync(sale.Id, _companyId);

        result.Should().BeOfType<Either<Error, NormalFiscalDocumentDto>.Left>();
        ((Either<Error, NormalFiscalDocumentDto>.Left)result).Value.Message.Should().Contain("could not be persisted");
        _unitOfWork.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IssueAsync_WhenTheSaleDoesNotExist_ReturnsLeft()
    {
        var result = await Service().IssueAsync(Guid.CreateVersion7(), _companyId);

        result.Should().BeOfType<Either<Error, NormalFiscalDocumentDto>.Left>();
        ((Either<Error, NormalFiscalDocumentDto>.Left)result).Value.Message.Should().Contain("does not exist");
    }

    [Fact]
    public async Task IssueAsync_WithoutAnId_ReturnsLeft()
    {
        var result = await Service().IssueAsync(Guid.Empty, _companyId);

        result.Should().BeOfType<Either<Error, NormalFiscalDocumentDto>.Left>();
        ((Either<Error, NormalFiscalDocumentDto>.Left)result).Value.Message.Should().Be("saleId must not be empty.");
    }

    [Fact]
    public async Task IssueAsync_WithoutACompany_ReturnsLeft()
    {
        var sale = GivenTheSale();

        var result = await Service().IssueAsync(sale.Id, Guid.Empty);

        result.Should().BeOfType<Either<Error, NormalFiscalDocumentDto>.Left>();
        ((Either<Error, NormalFiscalDocumentDto>.Left)result).Value.Message.Should().Be("companyId must not be empty.");
    }

    private NormalFiscalDocumentService Service() => new(_unitOfWork.Object);

    private void GivenTheSequence(int sequence)
    {
        _sequences.Setup(s => s.NextSequenceAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<NormalFiscalDocumentType>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(sequence);
    }

    private Sale GivenTheSale(EcfDocumentType documentType = EcfDocumentType.Consumer)
    {
        var sale = new Sale
        {
            Id = Guid.CreateVersion7(),
            CompanyId = _companyId,
            Code = "V-0001",
            Status = SaleStatus.Pending,
            DocumentType = documentType,
            ReceptorTaxId = "101010101",
            ClienteName = "Cliente de Prueba SRL"
        };

        sale.AddItem(Guid.CreateVersion7(), "Café molido", 2, 100m, TaxCode.ItbisTasa1);
        sale.CalculateTotals();

        _sales.Setup(s => s.GetSaleByIdAsync(sale.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sale);

        return sale;
    }
}
