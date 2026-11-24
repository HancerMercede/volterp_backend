using EitherWay;
using FluentAssertions;
using Moq;
using Volterp.Application.DTOs.NormalFiscalDocumentDtos;
using Volterp.Application.DTOs.SaleDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Interfaces;
using Volterp.Application.Services;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.ElectronicInvoicing;
using Volterp.Tests.TestHelpers;
using Xunit;

namespace Volterp.Tests.Services;

// The tax id of the buyer belongs to the sale: the cashier asks for it at the
// counter and writes it down, and the sale carries it. The registry of
// clients only fills the gap when the sale does not bring one.
public class SaleReceptorTaxIdTests
{
    private readonly Mock<IElectronicInvoiceService> _emissions = new();
    private readonly Mock<INormalFiscalDocumentService> _normalDocuments = new();

    public SaleReceptorTaxIdTests()
    {
        _normalDocuments.Setup(d => d.IssueAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Either<Error, NormalFiscalDocumentDto>.ToRight(new NormalFiscalDocumentDto
            {
                Id = Guid.CreateVersion7(),
                Number = "B0200000001",
                DocumentType = NormalFiscalDocumentType.Consumer
            }));
    }

    // ===== Create =====

    [Fact]
    public async Task CreateSaleAsync_WithTheTaxIdOfTheBuyer_KeepsItOnTheSale()
    {
        var (unitOfWork, _, _) = BuildUnitOfWork();
        SetupCodes(unitOfWork);
        var service = new SaleService(unitOfWork.Object, _emissions.Object, _normalDocuments.Object);

        var request = new CreateSaleRequest(
            ClienteId: null, ClienteName: "Comprador de mostrador",
            Status: SaleStatus.Pending, Total: 500, Notes: null,
            Items: [new CreateSaleItemRequest(Guid7TestValues.ProductA, "Test Product", null, null, null, 1, 100, 500)],
            ReceptorTaxId: "101010101"
        );

        var result = await service.CreateSaleAsync(request, Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        result.Should().BeOfType<Either<Error, SaleDto>.Right>();
        var dto = ((Either<Error, SaleDto>.Right)result).Value;
        dto.ReceptorTaxId.Should().Be("101010101");
    }

    [Fact]
    public async Task CreateSaleAsync_WithTheTaxIdOfTheBuyerAndAClientThatHasOne_KeepsTheTaxIdOfTheSale()
    {
        // The sale carries its own buyer: the registry does not overwrite it.
        var (unitOfWork, _, clients) = BuildUnitOfWork();
        SetupCodes(unitOfWork);
        clients.Setup(r => r.GetClientByIdAsync(Guid7TestValues.ClientA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Client
            {
                Id = Guid7TestValues.ClientA,
                CompanyId = Guid7TestValues.CompanyA,
                Name = "Test Client",
                TaxId = "999999999"
            });
        var service = new SaleService(unitOfWork.Object, _emissions.Object, _normalDocuments.Object);

        var request = new CreateSaleRequest(
            ClienteId: Guid7TestValues.ClientA, ClienteName: "Test Client",
            Status: SaleStatus.Pending, Total: 500, Notes: null,
            Items: [new CreateSaleItemRequest(Guid7TestValues.ProductA, "Test Product", null, null, null, 1, 100, 500)],
            ReceptorTaxId: "101010101"
        );

        var result = await service.CreateSaleAsync(request, Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        result.Should().BeOfType<Either<Error, SaleDto>.Right>();
        var dto = ((Either<Error, SaleDto>.Right)result).Value;
        dto.ReceptorTaxId.Should().Be("101010101");
    }

    [Fact]
    public async Task CreateSaleAsync_WithoutTheTaxIdOfTheBuyer_UsesTheTaxIdOfTheClient()
    {
        // The previous behavior: when the sale does not bring a buyer, the
        // one of the client fills the gap.
        var (unitOfWork, _, clients) = BuildUnitOfWork();
        SetupCodes(unitOfWork);
        clients.Setup(r => r.GetClientByIdAsync(Guid7TestValues.ClientA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Client
            {
                Id = Guid7TestValues.ClientA,
                CompanyId = Guid7TestValues.CompanyA,
                Name = "Test Client",
                TaxId = "101010101"
            });
        var service = new SaleService(unitOfWork.Object, _emissions.Object, _normalDocuments.Object);

        var request = new CreateSaleRequest(
            ClienteId: Guid7TestValues.ClientA, ClienteName: "Test Client",
            Status: SaleStatus.Pending, Total: 500, Notes: null,
            Items: [new CreateSaleItemRequest(Guid7TestValues.ProductA, "Test Product", null, null, null, 1, 100, 500)]
        );

        var result = await service.CreateSaleAsync(request, Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        result.Should().BeOfType<Either<Error, SaleDto>.Right>();
        var dto = ((Either<Error, SaleDto>.Right)result).Value;
        dto.ReceptorTaxId.Should().Be("101010101");
    }

    [Fact]
    public async Task CreateSaleAsync_WithTheTaxIdOfTheBuyer_TrimsTheSpaces()
    {
        var (unitOfWork, _, _) = BuildUnitOfWork();
        SetupCodes(unitOfWork);
        var service = new SaleService(unitOfWork.Object, _emissions.Object, _normalDocuments.Object);

        var request = new CreateSaleRequest(
            ClienteId: null, ClienteName: "Comprador de mostrador",
            Status: SaleStatus.Pending, Total: 500, Notes: null,
            Items: [new CreateSaleItemRequest(Guid7TestValues.ProductA, "Test Product", null, null, null, 1, 100, 500)],
            ReceptorTaxId: "  101010101  "
        );

        var result = await service.CreateSaleAsync(request, Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        result.Should().BeOfType<Either<Error, SaleDto>.Right>();
        var dto = ((Either<Error, SaleDto>.Right)result).Value;
        dto.ReceptorTaxId.Should().Be("101010101");
    }

    // ===== Update =====

    [Fact]
    public async Task UpdateSaleAsync_WithTheTaxIdOfTheBuyer_ReplacesTheTaxIdOfTheSale()
    {
        var (unitOfWork, sales, _) = BuildUnitOfWork();
        sales.Setup(r => r.GetSaleByIdAsync(Guid7TestValues.SaleA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSale(receptorTaxId: "999999999"));
        var service = new SaleService(unitOfWork.Object, _emissions.Object, _normalDocuments.Object);

        var request = new UpdateSaleRequest(
            ClienteId: null, ClienteName: "Comprador de mostrador",
            Status: SaleStatus.Pending, Total: 500, Notes: null,
            Items: [new CreateSaleItemRequest(Guid7TestValues.ProductA, "Test Product", null, null, null, 1, 100, 500)],
            ReceptorTaxId: "101010101"
        );

        var result = await service.UpdateSaleAsync(Guid7TestValues.SaleA, Guid7TestValues.CompanyA, request, Guid7TestValues.UserA);

        result.Should().BeOfType<Either<Error, SaleDto>.Right>();
        var dto = ((Either<Error, SaleDto>.Right)result).Value;
        dto.ReceptorTaxId.Should().Be("101010101");
    }

    [Fact]
    public async Task UpdateSaleAsync_WithoutTheTaxIdOfTheBuyer_KeepsTheTaxIdOfTheSale()
    {
        // A request that does not name a buyer does not erase the one the
        // sale already carries.
        var (unitOfWork, sales, _) = BuildUnitOfWork();
        sales.Setup(r => r.GetSaleByIdAsync(Guid7TestValues.SaleA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSale(receptorTaxId: "999999999"));
        var service = new SaleService(unitOfWork.Object, _emissions.Object, _normalDocuments.Object);

        var request = new UpdateSaleRequest(
            ClienteId: null, ClienteName: "Comprador de mostrador",
            Status: SaleStatus.Pending, Total: 500, Notes: null,
            Items: [new CreateSaleItemRequest(Guid7TestValues.ProductA, "Test Product", null, null, null, 1, 100, 500)]
        );

        var result = await service.UpdateSaleAsync(Guid7TestValues.SaleA, Guid7TestValues.CompanyA, request, Guid7TestValues.UserA);

        result.Should().BeOfType<Either<Error, SaleDto>.Right>();
        var dto = ((Either<Error, SaleDto>.Right)result).Value;
        dto.ReceptorTaxId.Should().Be("999999999");
    }

    [Fact]
    public async Task UpdateSaleAsync_WithoutTheTaxIdOnTheRequestAndWithoutOneOnTheSale_UsesTheTaxIdOfTheClient()
    {
        var (unitOfWork, sales, clients) = BuildUnitOfWork();
        sales.Setup(r => r.GetSaleByIdAsync(Guid7TestValues.SaleA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSale());
        clients.Setup(r => r.GetClientByIdAsync(Guid7TestValues.ClientA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Client
            {
                Id = Guid7TestValues.ClientA,
                CompanyId = Guid7TestValues.CompanyA,
                Name = "Test Client",
                TaxId = "101010101"
            });
        var service = new SaleService(unitOfWork.Object, _emissions.Object, _normalDocuments.Object);

        var request = new UpdateSaleRequest(
            ClienteId: Guid7TestValues.ClientA, ClienteName: "Test Client",
            Status: SaleStatus.Pending, Total: 500, Notes: null,
            Items: [new CreateSaleItemRequest(Guid7TestValues.ProductA, "Test Product", null, null, null, 1, 100, 500)]
        );

        var result = await service.UpdateSaleAsync(Guid7TestValues.SaleA, Guid7TestValues.CompanyA, request, Guid7TestValues.UserA);

        result.Should().BeOfType<Either<Error, SaleDto>.Right>();
        var dto = ((Either<Error, SaleDto>.Right)result).Value;
        dto.ReceptorTaxId.Should().Be("101010101");
    }

    // ===== Complete =====

    [Fact]
    public async Task CompleteSaleAsync_WithAConsumptionOf250000OrMoreAndNoTaxId_ReturnsTheError()
    {
        // From RD$250,000 the consumption invoice names its buyer: without
        // the tax id the document cannot leave.
        var (unitOfWork, sales, _) = BuildUnitOfWork();
        sales.Setup(r => r.GetSaleByIdAsync(Guid7TestValues.SaleA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSale(total: 250000m));
        var service = new SaleService(unitOfWork.Object, _emissions.Object, _normalDocuments.Object);

        var result = await service.CompleteSaleAsync(Guid7TestValues.SaleA, Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        result.Should().BeOfType<Either<Error, SaleDto>.Left>();
        var error = ((Either<Error, SaleDto>.Left)result).Value;
        error.Message.Should().Contain("receptor tax id");
    }

    [Fact]
    public async Task CompleteSaleAsync_WithAConsumptionBelow250000AndNoTaxId_CompletesTheSale()
    {
        // Below RD$250,000 the buyer is the final consumer: the sale goes
        // out without naming anyone.
        var (unitOfWork, sales, _) = BuildUnitOfWork();
        sales.Setup(r => r.GetSaleByIdAsync(Guid7TestValues.SaleA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSale(total: 249999.99m));
        var service = new SaleService(unitOfWork.Object, _emissions.Object, _normalDocuments.Object);

        var result = await service.CompleteSaleAsync(Guid7TestValues.SaleA, Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        result.Should().BeOfType<Either<Error, SaleDto>.Right>();
        var dto = ((Either<Error, SaleDto>.Right)result).Value;
        dto.Status.Should().Be(SaleStatus.Completed);
    }

    // ===== Helpers =====

    private static (Mock<IUnitOfWork> UnitOfWork, Mock<ISaleRepository> Sales, Mock<IClientRepository> Clients) BuildUnitOfWork()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        var sales = new Mock<ISaleRepository>();
        var products = new Mock<IProductRepository>();
        var clients = new Mock<IClientRepository>();

        products.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product>
            {
                new()
                {
                    Id = Guid7TestValues.ProductA, Stock = 10, CompanyId = Guid7TestValues.CompanyA,
                    Name = "Test Product", TaxCode = TaxCode.ItbisTasa1
                }
            });

        sales.Setup(r => r.AddSaleAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Sale s, CancellationToken ct) => s);
        sales.Setup(r => r.UpdateSaleAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        unitOfWork.Setup(u => u.Sales).Returns(sales.Object);
        unitOfWork.Setup(u => u.Products).Returns(products.Object);
        unitOfWork.Setup(u => u.Clients).Returns(clients.Object);
        unitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        return (unitOfWork, sales, clients);
    }

    private static void SetupCodes(Mock<IUnitOfWork> mockUnitOfWork, string code = "Vent-001")
    {
        var mockCodes = new Mock<ICodeGeneratorService>();
        mockCodes.Setup(c => c.NextAsync(It.IsAny<Guid>(), It.IsAny<CodeEntityType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(code);
        mockUnitOfWork.Setup(u => u.Codes).Returns(mockCodes.Object);
    }

    private static Sale CreateSale(string? receptorTaxId = null, decimal total = 500m)
    {
        var company = new Company
        {
            Id = Guid7TestValues.CompanyA,
            Name = "Test Company",
            TaxId = "130862346",
            Address = "1 A Street",
            LegalName = "Test Company Legal",
            Email = "a@company.test",
            Phone = "809-555-1234",
            IsActive = true,
            EmissionMode = CompanyEmissionMode.Normal
        };

        var sale = new Sale
        {
            Id = Guid7TestValues.SaleA,
            CompanyId = Guid7TestValues.CompanyA,
            Company = company,
            Code = "V-0001",
            Status = SaleStatus.Pending,
            Total = total,
            ReceptorTaxId = receptorTaxId,
            Items = new List<SaleItem>
            {
                new() { ProductId = Guid7TestValues.ProductA, ProductName = "Test Product", Quantity = 1, UnitPrice = 100, Subtotal = 100, TaxCode = TaxCode.ItbisTasa1 }
            }
        };

        return sale;
    }
}
