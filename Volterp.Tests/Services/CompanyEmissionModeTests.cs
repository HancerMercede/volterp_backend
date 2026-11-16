using EitherWay;
using FluentAssertions;
using Moq;
using Volterp.Application.DTOs.CompanyDtos.EmissionMode;
using Volterp.Application.DTOs.ElectronicInvoiceDtos;
using Volterp.Application.DTOs.NormalFiscalDocumentDtos;
using Volterp.Application.DTOs.SaleDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Interfaces;
using Volterp.Application.Services;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.ElectronicInvoicing;
using Xunit;

namespace Volterp.Tests.Services;

// The company chooses the fiscal path of its sales. Paper mode and an active
// contingency keep the electronic document out; they never rewrite a document
// that was already issued.
public class CompanyEmissionModeTests
{
    private static readonly DateTime Now = new(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid CompanyId = Guid.Parse("0192b9c7-b6a3-7def-8c5e-1f2a3b4c5d8e");
    private static readonly Guid SaleId = Guid.Parse("0192b9c7-b6a3-7def-8c5e-1f2a3b4c5d8f");

    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ISaleRepository> _sales = new();
    private readonly Mock<IProductRepository> _products = new();
    private readonly Mock<ICompanyRepository> _companies = new();
    private readonly Mock<IElectronicInvoiceRepository> _invoices = new();
    private readonly Mock<IElectronicInvoiceService> _issues = new();
    private readonly Mock<INormalFiscalDocumentService> _normalDocuments = new();

    private Company _company = null!;
    private Sale _sale = null!;

    public CompanyEmissionModeTests()
    {
        _unitOfWork.Setup(u => u.Sales).Returns(_sales.Object);
        _unitOfWork.Setup(u => u.Products).Returns(_products.Object);
        _unitOfWork.Setup(u => u.Companies).Returns(_companies.Object);
        _unitOfWork.Setup(u => u.ElectronicInvoices).Returns(_invoices.Object);
        _unitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _normalDocuments.Setup(d => d.IssueAsync(
                SaleId, CompanyId, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Either<Error, NormalFiscalDocumentDto>.ToRight(new NormalFiscalDocumentDto
            {
                Id = Guid.CreateVersion7(),
                CompanyId = CompanyId,
                SaleId = SaleId,
                Number = "B0200000001",
                DocumentType = NormalFiscalDocumentType.Consumer,
                IssuerTaxId = "130862346"
            }));

        _products.Setup(p => p.GetProductsByIdsAsync(
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _sales.Setup(s => s.UpdateSaleAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _issues.Setup(i => i.IssueAsync(SaleId, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Either<Error, ElectronicInvoiceDto>.ToRight(new ElectronicInvoiceDto
            {
                Id = Guid.CreateVersion7(),
                CompanyId = CompanyId,
                SaleId = SaleId,
                ENCF = "E320000000001",
                DocumentType = EcfDocumentType.Consumer,
                Status = EcfStatus.Signed
            }));
    }

    [Fact]
    public void ANewCompanyEmitsElectronicallyByDefault()
    {
        var company = new Company();

        company.EmissionMode.Should().Be(CompanyEmissionMode.Electronic,
            "electronic emission is the behavior the ERP already had");
        company.ContingencyStatus.Should().Be(CompanyContingencyStatus.None);
    }

    [Fact]
    public async Task ElectronicMode_IssuesTheElectronicDocument()
    {
        GivenCompany(CompanyEmissionMode.Electronic);

        await CompleteSaleAsync();

        _issues.Verify(i => i.IssueAsync(SaleId, CompanyId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PaperMode_DoesNotIssueAnElectronicDocument()
    {
        GivenCompany(CompanyEmissionMode.Paper);

        await CompleteSaleAsync();

        _issues.Verify(i => i.IssueAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _invoices.Verify(i => i.AddAsync(
            It.IsAny<ElectronicInvoice>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PaperMode_RecordsTheNormalDocumentOfTheSale()
    {
        GivenCompany(CompanyEmissionMode.Paper);

        await CompleteSaleAsync();

        // The paper document takes its number from the range the DGII
        // authorized for the normal documents.
        _normalDocuments.Verify(d => d.IssueAsync(
            SaleId, CompanyId, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(CompanyContingencyStatus.Total)]
    [InlineData(CompanyContingencyStatus.Partial)]
    public async Task AContingencyInsideItsWindow_SuspendsTheElectronicPath(
        CompanyContingencyStatus contingencyStatus)
    {
        GivenCompany(CompanyEmissionMode.Electronic, contingencyStatus, Now.AddHours(-1), Now.AddHours(1));

        await CompleteSaleAsync();

        _issues.Verify(i => i.IssueAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(CompanyContingencyStatus.Total)]
    [InlineData(CompanyContingencyStatus.Partial)]
    public async Task AContingencyThatHasNotStarted_DoesNotSuspendTheElectronicPath(
        CompanyContingencyStatus contingencyStatus)
    {
        GivenCompany(CompanyEmissionMode.Electronic, contingencyStatus, Now.AddHours(1), Now.AddHours(2));

        await CompleteSaleAsync();

        _issues.Verify(i => i.IssueAsync(SaleId, CompanyId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(CompanyContingencyStatus.Total)]
    [InlineData(CompanyContingencyStatus.Partial)]
    public async Task AContingencyThatHasEnded_DoesNotSuspendTheElectronicPath(
        CompanyContingencyStatus contingencyStatus)
    {
        GivenCompany(CompanyEmissionMode.Electronic, contingencyStatus, Now.AddHours(-2), Now.AddHours(-1));

        await CompleteSaleAsync();

        _issues.Verify(i => i.IssueAsync(SaleId, CompanyId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ElectronicMode_WhenTheCertificateIsMissing_DoesNotFallBackToPaper()
    {
        GivenCompany(CompanyEmissionMode.Electronic);
        _issues.Setup(i => i.IssueAsync(SaleId, CompanyId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(
                "There is no active signing certificate for the issuer RNC 130862346."));

        var result = await CompleteSaleAsync(expectsSuccess: false);

        result.Should().BeOfType<Either<Error, SaleDto>.Left>();
        _issues.Verify(i => i.IssueAsync(SaleId, CompanyId, It.IsAny<CancellationToken>()), Times.Once);
        _invoices.Verify(i => i.AddAsync(
            It.IsAny<ElectronicInvoice>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ADeactivatedCompany_StillTakesTheElectronicPath()
    {
        GivenCompany(CompanyEmissionMode.Electronic);
        _company.Deactivate();

        await CompleteSaleAsync();

        _issues.Verify(i => i.IssueAsync(SaleId, CompanyId, It.IsAny<CancellationToken>()), Times.Once,
            "the fiscal obligation does not disappear with the subscription");
    }

    [Fact]
    public async Task ChangingTheMode_DoesNotTouchTheDocumentsAlreadyIssued()
    {
        GivenCompany(CompanyEmissionMode.Electronic);
        _companies.Setup(c => c.GetCompanyByIdAsync(CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_company);
        _companies.Setup(c => c.UpdateCompanyAsync(It.IsAny<Company>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_company);

        var issued = new ElectronicInvoice
        {
            Id = Guid.CreateVersion7(),
            CompanyId = CompanyId,
            SaleId = SaleId,
            ENCF = "E320000000001",
            DocumentType = EcfDocumentType.Consumer,
            Status = EcfStatus.Accepted,
            SignedXml = "<ECF />"
        };

        var service = new CompanyEmissionModeService(_unitOfWork.Object);
        var result = await service.UpdateAsync(
            new UpdateCompanyEmissionModeDto(CompanyEmissionMode.Paper), CompanyId, Guid.CreateVersion7());

        result.Should().BeOfType<Either<Error, CompanyEmissionModeDto>.Right>();
        _company.EmissionMode.Should().Be(CompanyEmissionMode.Paper);
        issued.Status.Should().Be(EcfStatus.Accepted);
        issued.ENCF.Should().Be("E320000000001");
        _invoices.Verify(i => i.UpdateAsync(
            It.IsAny<ElectronicInvoice>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private void GivenCompany(
        CompanyEmissionMode emissionMode,
        CompanyContingencyStatus contingencyStatus = CompanyContingencyStatus.None,
        DateTime? contingencyStartsAt = null,
        DateTime? contingencyEndsAt = null)
    {
        _company = new Company
        {
            Id = CompanyId,
            Name = "Distribuidora del Norte",
            LegalName = "Distribuidora del Norte SRL",
            TaxId = "130862346",
            Address = "Calle Principal 1, Santo Domingo",
            Phone = "809-555-1234",
            Email = "facturacion@distribuidora.do",
            EmissionMode = emissionMode,
            ContingencyStatus = contingencyStatus,
            ContingencyStartsAt = contingencyStartsAt,
            ContingencyEndsAt = contingencyEndsAt
        };

        _sale = new Sale
        {
            Id = SaleId,
            CompanyId = CompanyId,
            Company = _company,
            Code = "V-0001",
            Status = SaleStatus.Pending,
            DocumentType = EcfDocumentType.Consumer,
            CreatedAt = Now.AddHours(-1)
        };
        _sale.AddItem(Guid.CreateVersion7(), "Cafe molido", 1, 100m, TaxCode.ItbisTasa1);

        _sales.Setup(s => s.GetSaleByIdAsync(SaleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_sale);

        // The paper document takes the RNC of the issuer from the company.
        _companies.Setup(c => c.GetCompanyByIdAsync(CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_company);
    }

    private async Task<Either<Error, SaleDto>> CompleteSaleAsync(bool expectsSuccess = true)
    {
        var service = new SaleService(
            _unitOfWork.Object,
            _issues.Object,
            _normalDocuments.Object,
            new FixedTimeProvider(Now));

        var result = await service.CompleteSaleAsync(SaleId, CompanyId, Guid.CreateVersion7());

        if (expectsSuccess)
            result.Should().BeOfType<Either<Error, SaleDto>.Right>();

        return result;
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);
    }
}
