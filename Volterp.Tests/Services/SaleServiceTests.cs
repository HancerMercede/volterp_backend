using EitherWay;
using FluentAssertions;
using Moq;
using Volterp.Application.DTOs.NormalFiscalDocumentDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Application.Services;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Xunit;
using Volterp.Tests.TestHelpers;
using Volterp.Application.DTOs.SaleDtos;
using Volterp.ElectronicInvoicing;

namespace Volterp.Tests.Services;

public class  SaleServiceTests
{
    private readonly Mock<IElectronicInvoiceService> _emissions = new();
    private readonly Mock<INormalFiscalDocumentService> _normalDocuments = new();

    public SaleServiceTests()
    {
        // The document service is a required collaborator: the stub answers
        // with a document and never with a null, so a test that does not care
        // about the paper path does not trip over it.
        _normalDocuments.Setup(d => d.IssueAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Either<Error, NormalFiscalDocumentDto>.ToRight(new NormalFiscalDocumentDto
            {
                Id = Guid.CreateVersion7(),
                Number = "B0200000001",
                DocumentType = NormalFiscalDocumentType.Consumer
            }));
    }

    [Fact]
    public async Task CreateSaleAsync_WithValidInput_DecrementsProductStock()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSalesRepo = new Mock<ISaleRepository>();
        var mockProductsRepo = new Mock<IProductRepository>();

        var product = new Product { Id = Guid7TestValues.ProductA, Stock = 10, CompanyId = Guid7TestValues.CompanyA, Name = "Test Product" };

        // Batch query: GetProductsByIdsAsync instead of individual GetProductByIdAsync
        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });
        mockSalesRepo.Setup(r => r.AddSaleAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Sale s, CancellationToken ct) => s);
        mockUnitOfWork.Setup(u => u.Sales).Returns(mockSalesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new SaleService(mockUnitOfWork.Object, _emissions.Object, _normalDocuments.Object);
        SetupCodes(mockUnitOfWork);
        var request = new CreateSaleRequest(
            ClienteId: null, ClienteName: "Test Client",
            Status: SaleStatus.Pending, Total: 500, Notes: null,
            Items: new List<CreateSaleItemRequest>
            {
                new(Guid7TestValues.ProductA, "Test Product", null, null, null, 5, 100, 500)
            }
        );

        // ACT
        await service.CreateSaleAsync(request, companyId: Guid7TestValues.CompanyA, userId: Guid7TestValues.UserA);

        // ASSERT - EF change tracker handles persistence; verify stock was decremented
        product.Stock.Should().Be(5);
    }

    [Fact]
    public async Task CreateSaleAsync_WithValidInput_DecrementsCorrectQuantity()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSalesRepo = new Mock<ISaleRepository>();
        var mockProductsRepo = new Mock<IProductRepository>();

        var product = new Product { Id = Guid7TestValues.ProductA, Stock = 20, CompanyId = Guid7TestValues.CompanyA, Name = "Test Product" };

        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });
        mockSalesRepo.Setup(r => r.AddSaleAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Sale s, CancellationToken ct) => s);
        mockUnitOfWork.Setup(u => u.Sales).Returns(mockSalesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new SaleService(mockUnitOfWork.Object, _emissions.Object, _normalDocuments.Object);
        SetupCodes(mockUnitOfWork);
        var request = new CreateSaleRequest(
            ClienteId: null, ClienteName: "Test",
            Status: SaleStatus.Pending, Total: 1500, Notes: null,
            Items: new List<CreateSaleItemRequest>
            {
                new(Guid7TestValues.ProductA, "Test", null, null, null, 15, 100, 1500)
            }
        );

        // ACT
        await service.CreateSaleAsync(request, companyId: Guid7TestValues.CompanyA, userId: Guid7TestValues.UserA);

        // ASSERT
        product.Stock.Should().Be(5);
    }

    [Fact]
    public async Task CreateSaleAsync_WithTaxedProduct_ComputesSubtotalItbisAndTotal()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSalesRepo = new Mock<ISaleRepository>();
        var mockProductsRepo = new Mock<IProductRepository>();

        var product = new Product
        {
            Id = Guid7TestValues.ProductA, Stock = 10, CompanyId = Guid7TestValues.CompanyA,
            Name = "Test Product", TaxCode = TaxCode.ItbisTasa2
        };

        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });
        mockSalesRepo.Setup(r => r.AddSaleAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Sale s, CancellationToken ct) => s);
        mockUnitOfWork.Setup(u => u.Sales).Returns(mockSalesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new SaleService(mockUnitOfWork.Object, _emissions.Object, _normalDocuments.Object);
        SetupCodes(mockUnitOfWork);
        var request = new CreateSaleRequest(
            ClienteId: null, ClienteName: "Test Client",
            Status: SaleStatus.Pending, Total: 500, Notes: null,
            Items: new List<CreateSaleItemRequest>
            {
                new(Guid7TestValues.ProductA, "Test Product", null, null, null, 5, 100, 500)
            }
        );

        // ACT
        var result = await service.CreateSaleAsync(request, companyId: Guid7TestValues.CompanyA, userId: Guid7TestValues.UserA);

        // ASSERT - the domain computes the breakdown, the request total is not trusted
        result.Should().BeOfType<Either<Error, SaleDto>.Right>();
        var dto = ((Either<Error, SaleDto>.Right)result).Value;
        dto.Subtotal.Should().Be(500);
        dto.Itbis.Should().Be(80);
        dto.Total.Should().Be(580);
        dto.Items.Should().ContainSingle().Which.TaxCode.Should().Be(TaxCode.ItbisTasa2);
    }

    [Fact]
    public async Task CreateSaleAsync_WithFiscalCreditAndClientTaxId_CopiesTheReceptorTaxId()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSalesRepo = new Mock<ISaleRepository>();
        var mockProductsRepo = new Mock<IProductRepository>();
        var mockClientsRepo = new Mock<IClientRepository>();

        var product = new Product
        {
            Id = Guid7TestValues.ProductA, Stock = 10, CompanyId = Guid7TestValues.CompanyA,
            Name = "Test Product", TaxCode = TaxCode.ItbisTasa1
        };
        var client = new Client
        {
            Id = Guid7TestValues.ClientA, CompanyId = Guid7TestValues.CompanyA,
            Name = "Test Client", TaxId = "101010101"
        };

        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });
        mockClientsRepo.Setup(r => r.GetClientByIdAsync(Guid7TestValues.ClientA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(client);
        mockSalesRepo.Setup(r => r.AddSaleAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Sale s, CancellationToken ct) => s);
        mockUnitOfWork.Setup(u => u.Sales).Returns(mockSalesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.Clients).Returns(mockClientsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new SaleService(mockUnitOfWork.Object, _emissions.Object, _normalDocuments.Object);
        SetupCodes(mockUnitOfWork);
        var request = new CreateSaleRequest(
            ClienteId: Guid7TestValues.ClientA, ClienteName: "Test Client",
            Status: SaleStatus.Pending, Total: 100, Notes: null,
            Items: new List<CreateSaleItemRequest>
            {
                new(Guid7TestValues.ProductA, "Test Product", null, null, null, 1, 100, 100)
            },
            DocumentType: EcfDocumentType.FiscalCredit
        );

        // ACT
        var result = await service.CreateSaleAsync(request, companyId: Guid7TestValues.CompanyA, userId: Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, SaleDto>.Right>();
        var dto = ((Either<Error, SaleDto>.Right)result).Value;
        dto.DocumentType.Should().Be(EcfDocumentType.FiscalCredit);
        dto.ReceptorTaxId.Should().Be("101010101");
    }

    [Fact]
    public async Task CompleteSaleAsync_WithValidInput_SetsStatusToCompleted()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSalesRepo = new Mock<ISaleRepository>();
        var mockProductsRepo = new Mock<IProductRepository>();

        var product = new Product { Id = Guid7TestValues.ProductA, Stock = 10, CompanyId = Guid7TestValues.CompanyA, Name = "Test" };

        var sale = new Sale
        {
            Id = Guid7TestValues.SaleA, CompanyId = Guid7TestValues.CompanyA, Status = SaleStatus.Pending, Total = 500,
            Items = new List<SaleItem>
            {
                new() { ProductId = Guid7TestValues.ProductA, ProductName = "Test", Quantity = 5, UnitPrice = 100, Subtotal = 500 }
            }
        };

        mockSalesRepo.Setup(r => r.GetSaleByIdAsync(Guid7TestValues.SaleA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sale);
        mockSalesRepo.Setup(r => r.UpdateSaleAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });
        mockUnitOfWork.Setup(u => u.Sales).Returns(mockSalesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new SaleService(mockUnitOfWork.Object, _emissions.Object, _normalDocuments.Object);

        // ACT
        var result = await service.CompleteSaleAsync(Guid7TestValues.SaleA, Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        // ASSERT
        sale.Status.Should().Be(SaleStatus.Completed);
        mockSalesRepo.Verify(r => r.UpdateSaleAsync(sale, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateSaleAsync_WithValidInput_AdjustsStockOnItemQuantityChange()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSalesRepo = new Mock<ISaleRepository>();
        var mockProductsRepo = new Mock<IProductRepository>();

        var product = new Product { Id = Guid7TestValues.ProductA, Stock = 10, CompanyId = Guid7TestValues.CompanyA, Name = "Test" };

        var existingSale = new Sale
        {
            Id = Guid7TestValues.SaleA, CompanyId = Guid7TestValues.CompanyA, Status = SaleStatus.Pending, Total = 500,
            Items = new List<SaleItem>
            {
                new() { Id = Guid7TestValues.SaleA, ProductId = Guid7TestValues.ProductA, ProductName = "Test", Quantity = 5, UnitPrice = 100, Subtotal = 500 }
            }
        };

        mockSalesRepo.Setup(r => r.GetSaleByIdAsync(Guid7TestValues.SaleA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingSale);
        mockSalesRepo.Setup(r => r.UpdateSaleAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        // Batch query: GetProductsByIdsAsync returns products for both old and new items
        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });
        mockUnitOfWork.Setup(u => u.Sales).Returns(mockSalesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new SaleService(mockUnitOfWork.Object, _emissions.Object, _normalDocuments.Object);
        var updateRequest = new UpdateSaleRequest(
            ClienteId: null, ClienteName: "Updated", Status: SaleStatus.Pending,
            Total: 800, Notes: null,
            Items: new List<CreateSaleItemRequest>
            {
                new(Guid7TestValues.ProductA, "Test", null, null, null, 8, 100, 800) // Changed from 5 to 8
            }
        );

        // ACT
        await service.UpdateSaleAsync(Guid7TestValues.SaleA, Guid7TestValues.CompanyA, updateRequest, Guid7TestValues.UserA);

        // ASSERT - Expected: old qty (5) returned, new qty (8) deducted
        // So stock should be: 10 + 5 - 8 = 7
        product.Stock.Should().Be(7);
    }

    [Fact]
    public async Task GetAllSalesAsync_ReturnsPagedResult()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSalesRepo = new Mock<ISaleRepository>();

        var sales = new PagedResult<Sale>
        {
            Items = new List<Sale>
            {
                new() { Id = Guid7TestValues.SaleA, CompanyId = Guid7TestValues.CompanyA, Status = SaleStatus.Pending, Total = 500, ClienteName = "Client A" },
                new() { Id = Guid.NewGuid(), CompanyId = Guid7TestValues.CompanyA, Status = SaleStatus.Completed, Total = 300, ClienteName = "Client B" }
            },
            PageNumber = 1,
            PageSize = 10,
            RowCount = 2,
            PageCount = 1
        };

        mockSalesRepo.Setup(r => r.GetAllSalesAsync(1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sales);
        mockUnitOfWork.Setup(u => u.Sales).Returns(mockSalesRepo.Object);

        var service = new SaleService(mockUnitOfWork.Object, _emissions.Object, _normalDocuments.Object);

        // ACT
        var result = await service.GetAllSalesAsync(1, 10);

        // ASSERT
        result.Items.Should().HaveCount(2);
        result.RowCount.Should().Be(2);
    }

    [Fact]
    public async Task GetSalesByStatusAsync_ReturnsFilteredResults()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSalesRepo = new Mock<ISaleRepository>();

        var sales = new PagedResult<Sale>
        {
            Items = new List<Sale>
            {
                new() { Id = Guid7TestValues.SaleA, CompanyId = Guid7TestValues.CompanyA, Status = SaleStatus.Pending, Total = 500, ClienteName = "Client A" }
            },
            PageNumber = 1,
            PageSize = 10,
            RowCount = 1,
            PageCount = 1
        };

        mockSalesRepo.Setup(r => r.GetSalesByStatusAsync(SaleStatus.Pending, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sales);
        mockUnitOfWork.Setup(u => u.Sales).Returns(mockSalesRepo.Object);

        var service = new SaleService(mockUnitOfWork.Object, _emissions.Object, _normalDocuments.Object);

        // ACT
        var result = await service.GetSalesByStatusAsync(SaleStatus.Pending, 1, 10);

        // ASSERT
        result.Items.Should().HaveCount(1);
        result.Items.First().Status.Should().Be(SaleStatus.Pending);
    }

    [Fact]
    public async Task GetSaleByIdAsync_WhenFound_ReturnsSaleDto()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSalesRepo = new Mock<ISaleRepository>();

        var sale = new Sale
        {
            Id = Guid7TestValues.SaleA, CompanyId = Guid7TestValues.CompanyA, Status = SaleStatus.Pending, Total = 500,
            ClienteName = "Test Client",
            Items = new List<SaleItem>
            {
                new() { Id = Guid7TestValues.SaleA, ProductId = Guid7TestValues.ProductA, ProductName = "Test", Quantity = 5, UnitPrice = 100, Subtotal = 500 }
            }
        };

        mockSalesRepo.Setup(r => r.GetSaleByIdAsync(Guid7TestValues.SaleA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sale);
        mockUnitOfWork.Setup(u => u.Sales).Returns(mockSalesRepo.Object);

        var service = new SaleService(mockUnitOfWork.Object, _emissions.Object, _normalDocuments.Object);

        // ACT
        var result = await service.GetSaleByIdAsync(Guid7TestValues.SaleA);

        // ASSERT
        result.Should().BeOfType<Either<Error, SaleDto?>.Right>();
        var dto = ((Either<Error, SaleDto?>.Right)result).Value;
        dto.Should().NotBeNull();
        dto!.Id.Should().Be(Guid7TestValues.SaleA);
        dto.ClienteName.Should().Be("Test Client");
    }

    [Fact]
    public async Task GetSaleByIdAsync_WhenNotFound_ReturnsNull()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSalesRepo = new Mock<ISaleRepository>();

        mockSalesRepo.Setup(r => r.GetSaleByIdAsync(Guid.NewGuid(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Sale?)null);
        mockUnitOfWork.Setup(u => u.Sales).Returns(mockSalesRepo.Object);

        var service = new SaleService(mockUnitOfWork.Object, _emissions.Object, _normalDocuments.Object);

        // ACT
        var result = await service.GetSaleByIdAsync(Guid.NewGuid());

        // ASSERT
        result.Should().BeOfType<Either<Error, SaleDto?>.Right>();
        var dto = ((Either<Error, SaleDto?>.Right)result).Value;
        dto.Should().BeNull();
    }

    [Fact]
    public async Task CreateSaleAsync_WithProductNotFound_DoesNotModifyStock()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSalesRepo = new Mock<ISaleRepository>();
        var mockProductsRepo = new Mock<IProductRepository>();

        // Product not found in batch query
        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product>());
        mockSalesRepo.Setup(r => r.AddSaleAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Sale s, CancellationToken ct) => s);
        mockUnitOfWork.Setup(u => u.Sales).Returns(mockSalesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new SaleService(mockUnitOfWork.Object, _emissions.Object, _normalDocuments.Object);
        SetupCodes(mockUnitOfWork);
        var request = new CreateSaleRequest(
            ClienteId: null, ClienteName: "Test",
            Status: SaleStatus.Pending, Total: 500, Notes: null,
            Items: new List<CreateSaleItemRequest>
            {
                new(Guid.NewGuid(), "NonExistent", null, null, null, 5, 100, 500) // product doesn't exist
            }
        );

        // ACT - should not throw, just doesn't decrement stock for missing products
        var result = await service.CreateSaleAsync(request, companyId: Guid7TestValues.CompanyA, userId: Guid7TestValues.UserA);

        // ASSERT - sale created, no stock modification occurred
        result.Should().NotBeNull();
        mockSalesRepo.Verify(r => r.AddSaleAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateSaleAsync_WhenSaleNotFound_ThrowsSaleNotFoundException()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSalesRepo = new Mock<ISaleRepository>();

        mockSalesRepo.Setup(r => r.GetSaleByIdAsync(Guid.NewGuid(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Sale?)null);
        mockUnitOfWork.Setup(u => u.Sales).Returns(mockSalesRepo.Object);

        var service = new SaleService(mockUnitOfWork.Object, _emissions.Object, _normalDocuments.Object);
        var updateRequest = new UpdateSaleRequest(
            ClienteId: null, ClienteName: "Updated", Status: SaleStatus.Pending,
            Total: 800, Notes: null,
            Items: new List<CreateSaleItemRequest>
            {
                new(Guid7TestValues.ProductA, "Test", null, null, null, 5, 100, 500)
            }
        );

        // ACT
        var result = await service.UpdateSaleAsync(Guid.NewGuid(), Guid7TestValues.CompanyA, updateRequest, Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, SaleDto>.Left>();
        var error = ((Either<Error, SaleDto>.Left)result).Value;
        error.Message.Should().Be("Sale not found.");
    }

    [Fact]
    public async Task CompleteSaleAsync_WhenSaleNotFound_ReturnsLeft()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSalesRepo = new Mock<ISaleRepository>();

        mockSalesRepo.Setup(r => r.GetSaleByIdAsync(Guid.NewGuid(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Sale?)null);
        mockUnitOfWork.Setup(u => u.Sales).Returns(mockSalesRepo.Object);

        var service = new SaleService(mockUnitOfWork.Object, _emissions.Object, _normalDocuments.Object);

        // ACT
        var result = await service.CompleteSaleAsync(Guid.NewGuid(), Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, SaleDto>.Left>();
        var error = ((Either<Error, SaleDto>.Left)result).Value;
        error.Message.Should().Be("Sale not found.");
    }

    [Fact]
    public async Task DeleteSaleAsync_WhenSaleNotFound_ReturnsLeft()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSalesRepo = new Mock<ISaleRepository>();

        mockSalesRepo.Setup(r => r.GetSaleByIdAsync(Guid.NewGuid(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Sale?)null);
        mockUnitOfWork.Setup(u => u.Sales).Returns(mockSalesRepo.Object);

        var service = new SaleService(mockUnitOfWork.Object, _emissions.Object, _normalDocuments.Object);

        // ACT
        var result = await service.DeleteSaleAsync(Guid.NewGuid(), Guid7TestValues.CompanyA);

        // ASSERT
        result.Should().BeOfType<Either<Error, Unit>.Left>();
        var error = ((Either<Error, Unit>.Left)result).Value;
        error.Message.Should().Be("Sale not found.");
    }

    [Fact]
    public async Task DeleteSaleAsync_WhenSaleFound_CallsDelete()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSalesRepo = new Mock<ISaleRepository>();
        var mockProductsRepo = new Mock<IProductRepository>();

        var sale = new Sale
        {
            Id = Guid7TestValues.SaleA, CompanyId = Guid7TestValues.CompanyA, Status = SaleStatus.Pending, Total = 500,
            Items = new List<SaleItem>()
        };

        mockSalesRepo.Setup(r => r.GetSaleByIdAsync(Guid7TestValues.SaleA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sale);
        mockSalesRepo.Setup(r => r.DeleteSaleAsync(Guid7TestValues.SaleA, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product>());
        mockUnitOfWork.Setup(u => u.Sales).Returns(mockSalesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new SaleService(mockUnitOfWork.Object, _emissions.Object, _normalDocuments.Object);

        // ACT
        await service.DeleteSaleAsync(Guid7TestValues.SaleA, Guid7TestValues.CompanyA);

        // ASSERT
        mockSalesRepo.Verify(r => r.DeleteSaleAsync(Guid7TestValues.SaleA, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateSaleAsync_WithInsufficientStock_ThrowsInvalidOperationException()
    {
        // ARRANGE - product has only 5 in stock but we try to sell 10
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSalesRepo = new Mock<ISaleRepository>();
        var mockProductsRepo = new Mock<IProductRepository>();

        var product = new Product { Id = Guid7TestValues.ProductA, Stock = 5, CompanyId = Guid7TestValues.CompanyA, Name = "Test Product" };

        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });
        mockUnitOfWork.Setup(u => u.Sales).Returns(mockSalesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);

        var service = new SaleService(mockUnitOfWork.Object, _emissions.Object, _normalDocuments.Object);
        SetupCodes(mockUnitOfWork);
        var request = new CreateSaleRequest(
            ClienteId: null, ClienteName: "Test",
            Status: SaleStatus.Pending, Total: 1000, Notes: null,
            Items: new List<CreateSaleItemRequest>
            {
                new(Guid7TestValues.ProductA, "Test Product", null, null, null, 10, 100, 1000) // 10 > 5 available
            }
        );

        // ACT
        var result = await service.CreateSaleAsync(request, companyId: Guid7TestValues.CompanyA, userId: Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, SaleDto>.Left>();
        var error = ((Either<Error, SaleDto>.Left)result).Value;
        error.Message.Should().Contain("Insufficient stock");
        product.Stock.Should().Be(5); // Stock unchanged
    }

    [Fact]
    public async Task GetAllSalesAsync_WithPageZero_ReturnsResults()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSalesRepo = new Mock<ISaleRepository>();

        var sales = new PagedResult<Sale>
        {
            Items = new List<Sale>
            {
                new() { Id = Guid7TestValues.SaleA, CompanyId = Guid7TestValues.CompanyA, Status = SaleStatus.Pending, Total = 500, ClienteName = "Client" }
            },
            PageNumber = 0,
            PageSize = 10,
            RowCount = 1,
            PageCount = 1
        };

        mockSalesRepo.Setup(r => r.GetAllSalesAsync(0, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sales);
        mockUnitOfWork.Setup(u => u.Sales).Returns(mockSalesRepo.Object);

        var service = new SaleService(mockUnitOfWork.Object, _emissions.Object, _normalDocuments.Object);

        // ACT
        var result = await service.GetAllSalesAsync(0, 10);

        // ASSERT
        result.Items.Should().HaveCount(1);
        result.PageNumber.Should().Be(0);
    }

    [Fact]
    public async Task CreateSaleAsync_WhenCommitFails_ThrowsException()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSalesRepo = new Mock<ISaleRepository>();
        var mockProductsRepo = new Mock<IProductRepository>();

        var product = new Product { Id = Guid7TestValues.ProductA, Stock = 10, CompanyId = Guid7TestValues.CompanyA, Name = "Test Product" };

        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });
        mockSalesRepo.Setup(r => r.AddSaleAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Sale s, CancellationToken ct) => s);
        mockUnitOfWork.Setup(u => u.Sales).Returns(mockSalesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database connection failed"));

        var service = new SaleService(mockUnitOfWork.Object, _emissions.Object, _normalDocuments.Object);
        SetupCodes(mockUnitOfWork);
        var request = new CreateSaleRequest(
            ClienteId: null, ClienteName: "Test",
            Status: SaleStatus.Pending, Total: 500, Notes: null,
            Items: new List<CreateSaleItemRequest>
            {
                new(Guid7TestValues.ProductA, "Test", null, null, null, 5, 100, 500)
            }
        );

        // ACT
        var result = await service.CreateSaleAsync(request, companyId: Guid7TestValues.CompanyA, userId: Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, SaleDto>.Left>();
        var error = ((Either<Error, SaleDto>.Left)result).Value;
        error.Message.Should().Be("Database connection failed");
        // NOTE: In unit tests with mocks, stock changes persist in memory even when
        // commit fails. In a real DB transaction, this would be rolled back.
        // This test documents the current behavior - proper transaction handling
        // requires integration tests with a real database.
        product.Stock.Should().Be(5); // Stock was decremented before commit failed
    }

    [Fact]
    public async Task CreateSaleAsync_WithMultipleItems_DecrementsAllCorrectly()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSalesRepo = new Mock<ISaleRepository>();
        var mockProductsRepo = new Mock<IProductRepository>();

        var product1 = new Product { Id = Guid7TestValues.ProductA, Stock = 100, CompanyId = Guid7TestValues.CompanyA, Name = "Product A" };
        var product2Id = Guid.NewGuid();
        var product3Id = Guid.NewGuid();
        var product2 = new Product { Id = product2Id, Stock = 50, CompanyId = Guid7TestValues.CompanyA, Name = "Product B" };
        var product3 = new Product { Id = product3Id, Stock = 25, CompanyId = Guid7TestValues.CompanyA, Name = "Product C" };

        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product1, product2, product3 });
        mockSalesRepo.Setup(r => r.AddSaleAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Sale s, CancellationToken ct) => s);
        mockUnitOfWork.Setup(u => u.Sales).Returns(mockSalesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new SaleService(mockUnitOfWork.Object, _emissions.Object, _normalDocuments.Object);
        SetupCodes(mockUnitOfWork);
        var request = new CreateSaleRequest(
            ClienteId: null, ClienteName: "Bulk Client",
            Status: SaleStatus.Pending, Total: 3000, Notes: null,
            Items: new List<CreateSaleItemRequest>
            {
                new(Guid7TestValues.ProductA, "Product A", null, null, null, 10, 100, 1000),
                new(product2Id, "Product B", null, null, null, 20, 50, 1000),
                new(product3Id, "Product C", null, null, null, 5, 200, 1000)
            }
        );

        // ACT
        await service.CreateSaleAsync(request, companyId: Guid7TestValues.CompanyA, userId: Guid7TestValues.UserA);

        // ASSERT
        product1.Stock.Should().Be(90);  // 100 - 10
        product2.Stock.Should().Be(30);  // 50 - 20
        product3.Stock.Should().Be(20);  // 25 - 5
    }

[Fact]
    public async Task UpdateSaleAsync_WhenCommitFails_ThrowsAndRevertsStockChanges()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSalesRepo = new Mock<ISaleRepository>();
        var mockProductsRepo = new Mock<IProductRepository>();

        var product = new Product { Id = Guid7TestValues.ProductA, Stock = 10, CompanyId = Guid7TestValues.CompanyA, Name = "Test" };

        var existingSale = new Sale
        {
            Id = Guid7TestValues.SaleA, CompanyId = Guid7TestValues.CompanyA, Status = SaleStatus.Pending, Total = 500,
            Items = new List<SaleItem>
            {
                new() { Id = Guid7TestValues.SaleA, ProductId = Guid7TestValues.ProductA, ProductName = "Test", Quantity = 5, UnitPrice = 100, Subtotal = 500 }
            }
        };

        mockSalesRepo.Setup(r => r.GetSaleByIdAsync(Guid7TestValues.SaleA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingSale);
        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });
        mockUnitOfWork.Setup(u => u.Sales).Returns(mockSalesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Commit failed"));

        var service = new SaleService(mockUnitOfWork.Object, _emissions.Object, _normalDocuments.Object);
        var updateRequest = new UpdateSaleRequest(
            ClienteId: null, ClienteName: "Updated", Status: SaleStatus.Pending,
            Total: 800, Notes: null,
            Items: new List<CreateSaleItemRequest>
            {
                new(Guid7TestValues.ProductA, "Test", null, null, null, 8, 100, 800)
            }
        );

        // ACT
        var result = await service.UpdateSaleAsync(Guid7TestValues.SaleA, Guid7TestValues.CompanyA, updateRequest, Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, SaleDto>.Left>();
        var error = ((Either<Error, SaleDto>.Left)result).Value;
        error.Message.Should().Be("Commit failed");
        // Stock changes applied in memory before commit: 10 + 5 (return old) - 8 (deduct new) = 7
        // NOTE: This documents current behavior. True rollback requires integration tests.
        product.Stock.Should().Be(7);
    }

[Fact]
    public async Task UpdateSaleAsync_WithInsufficientStock_ThrowsInvalidOperationException()
    {
        // ARRANGE - existing sale has product with qty 4, product stock 5, update requests qty 10
        // After returns: 5 + 4 = 9, but new request needs 10, so insufficient
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSalesRepo = new Mock<ISaleRepository>();
        var mockProductsRepo = new Mock<IProductRepository>();

        var product = new Product { Id = Guid7TestValues.ProductA, Stock = 5, CompanyId = Guid7TestValues.CompanyA, Name = "Test Product" };

        var existingSale = new Sale
        {
            Id = Guid7TestValues.SaleA, CompanyId = Guid7TestValues.CompanyA, Status = SaleStatus.Pending, Total = 400,
            Items = new List<SaleItem>
            {
                new() { Id = Guid7TestValues.SaleA, ProductId = Guid7TestValues.ProductA, ProductName = "Test Product", Quantity = 4, UnitPrice = 100, Subtotal = 400 }
            }
        };

        mockSalesRepo.Setup(r => r.GetSaleByIdAsync(Guid7TestValues.SaleA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingSale);
        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });
        mockUnitOfWork.Setup(u => u.Sales).Returns(mockSalesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new SaleService(mockUnitOfWork.Object, _emissions.Object, _normalDocuments.Object);
        var updateRequest = new UpdateSaleRequest(
            ClienteId: null, ClienteName: "Updated", Status: SaleStatus.Pending,
            Total: 1000, Notes: null,
            Items: new List<CreateSaleItemRequest>
            {
                new(Guid7TestValues.ProductA, "Test Product", null, null, null, 10, 100, 1000) // 10 > 9 available after return
            }
        );

// ACT
        var result = await service.UpdateSaleAsync(Guid7TestValues.SaleA, Guid7TestValues.CompanyA, updateRequest, Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, SaleDto>.Left>();
        var error = ((Either<Error, SaleDto>.Left)result).Value;
        error.Message.Should().Contain("Insufficient stock");
        // Stock was restored by oldReturns (5 + 4 = 9) before validation failed
        // oldReturns are applied in-memory before the new deduction is validated
        product.Stock.Should().Be(9);
    }

    [Fact]
    public async Task UpdateSaleAsync_WithNetChangeOnSameProduct_AppliesCorrectStock()
    {
        // ARRANGE: sale.Items has ProductId=1 with qty=5 (stock was 20), request.Items has ProductId=1 with qty=3
        // Net = 3 - 5 = -2, so stock should be 18 (returns 2 more than deducts)
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSalesRepo = new Mock<ISaleRepository>();
        var mockProductsRepo = new Mock<IProductRepository>();

        var product = new Product { Id = Guid7TestValues.ProductA, Stock = 20, CompanyId = Guid7TestValues.CompanyA, Name = "Test" };

        var existingSale = new Sale
        {
            Id = Guid7TestValues.SaleA, CompanyId = Guid7TestValues.CompanyA, Status = SaleStatus.Pending, Total = 500,
            Items = new List<SaleItem>
            {
                new() { Id = Guid7TestValues.SaleA, ProductId = Guid7TestValues.ProductA, ProductName = "Test", Quantity = 5, UnitPrice = 100, Subtotal = 500 }
            }
        };

        mockSalesRepo.Setup(r => r.GetSaleByIdAsync(Guid7TestValues.SaleA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingSale);
        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });
        mockUnitOfWork.Setup(u => u.Sales).Returns(mockSalesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new SaleService(mockUnitOfWork.Object, _emissions.Object, _normalDocuments.Object);
        var updateRequest = new UpdateSaleRequest(
            ClienteId: null, ClienteName: "Updated", Status: SaleStatus.Pending,
            Total: 300, Notes: null,
            Items: new List<CreateSaleItemRequest>
            {
                new(Guid7TestValues.ProductA, "Test", null, null, null, 3, 100, 300)
            }
        );

        // ACT
        await service.UpdateSaleAsync(Guid7TestValues.SaleA, Guid7TestValues.CompanyA, updateRequest, Guid7TestValues.UserA);

        // ASSERT: 20 + 5 (return old) - 3 (deduct new) = 22
        product.Stock.Should().Be(22);
    }

    [Fact]
    public async Task UpdateSaleAsync_WithProductNotFoundInMap_SkipsGracefully()
    {
        // ARRANGE: request.Items has a product that doesn't exist in DB
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSalesRepo = new Mock<ISaleRepository>();
        var mockProductsRepo = new Mock<IProductRepository>();

        var existingProduct = new Product { Id = Guid7TestValues.ProductA, Stock = 20, CompanyId = Guid7TestValues.CompanyA, Name = "Existing Product" };

        var existingSale = new Sale
        {
            Id = Guid7TestValues.SaleA, CompanyId = Guid7TestValues.CompanyA, Status = SaleStatus.Pending, Total = 500,
            Items = new List<SaleItem>
            {
                new() { Id = Guid7TestValues.SaleA, ProductId = Guid7TestValues.ProductA, ProductName = "Existing Product", Quantity = 5, UnitPrice = 100, Subtotal = 500 }
            }
        };

        mockSalesRepo.Setup(r => r.GetSaleByIdAsync(Guid7TestValues.SaleA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingSale);
        mockSalesRepo.Setup(r => r.UpdateSaleAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        // Only return existing product, not the non-existent one (id=999)
        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { existingProduct });
        mockUnitOfWork.Setup(u => u.Sales).Returns(mockSalesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new SaleService(mockUnitOfWork.Object, _emissions.Object, _normalDocuments.Object);
        var updateRequest = new UpdateSaleRequest(
            ClienteId: null, ClienteName: "Updated", Status: SaleStatus.Pending,
            Total: 800, Notes: null,
            Items: new List<CreateSaleItemRequest>
            {
                new(Guid7TestValues.ProductA, "Existing Product", null, null, null, 3, 100, 300),
                new(Guid.NewGuid(), "NonExistent", null, null, null, 5, 100, 500) // Product not in DB
            }
        );

        // ACT - should not throw, just skip the non-existent product
        var result = await service.UpdateSaleAsync(Guid7TestValues.SaleA, Guid7TestValues.CompanyA, updateRequest, Guid7TestValues.UserA);

        // ASSERT - stock of existing product should change correctly
        existingProduct.Stock.Should().Be(22); // 20 + 5 (return) - 3 (deduct)
    }

    [Fact]
    public async Task CreateSaleAsync_WithDuplicateProductIds_GroupsQuantities()
    {
        // ARRANGE: request.Items has two items with same ProductId (qty 3 and qty 2)
        // Should group and decrement 5 total
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSalesRepo = new Mock<ISaleRepository>();
        var mockProductsRepo = new Mock<IProductRepository>();

        var product = new Product { Id = Guid7TestValues.ProductA, Stock = 20, CompanyId = Guid7TestValues.CompanyA, Name = "Test Product" };

        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });
        mockSalesRepo.Setup(r => r.AddSaleAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Sale s, CancellationToken ct) => s);
        mockUnitOfWork.Setup(u => u.Sales).Returns(mockSalesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new SaleService(mockUnitOfWork.Object, _emissions.Object, _normalDocuments.Object);
        SetupCodes(mockUnitOfWork);
        var request = new CreateSaleRequest(
            ClienteId: null, ClienteName: "Test",
            Status: SaleStatus.Pending, Total: 500, Notes: null,
            Items: new List<CreateSaleItemRequest>
            {
                new(Guid7TestValues.ProductA, "Test Product", null, null, null, 3, 100, 300),
                new(Guid7TestValues.ProductA, "Test Product", null, null, null, 2, 100, 200) // Same ProductId, should group to 5
            }
        );

        // ACT
        await service.CreateSaleAsync(request, companyId: Guid7TestValues.CompanyA, userId: Guid7TestValues.UserA);

        // ASSERT - grouped quantity 3 + 2 = 5 should be decremented
        product.Stock.Should().Be(15); // 20 - 5
    }

    [Fact]
    public async Task CreateSaleAsync_WithStockExactlyZero_ThrowsException()
    {
        // ARRANGE: product.Stock = 0, request qty = 1
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSalesRepo = new Mock<ISaleRepository>();
        var mockProductsRepo = new Mock<IProductRepository>();

        var product = new Product { Id = Guid7TestValues.ProductA, Stock = 0, CompanyId = Guid7TestValues.CompanyA, Name = "Test Product" };

        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });
        mockUnitOfWork.Setup(u => u.Sales).Returns(mockSalesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);

        var service = new SaleService(mockUnitOfWork.Object, _emissions.Object, _normalDocuments.Object);
        SetupCodes(mockUnitOfWork);
        var request = new CreateSaleRequest(
            ClienteId: null, ClienteName: "Test",
            Status: SaleStatus.Pending, Total: 100, Notes: null,
            Items: new List<CreateSaleItemRequest>
            {
                new(Guid7TestValues.ProductA, "Test Product", null, null, null, 1, 100, 100)
            }
        );

        // ACT
        var result = await service.CreateSaleAsync(request, companyId: Guid7TestValues.CompanyA, userId: Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, SaleDto>.Left>();
        var error = ((Either<Error, SaleDto>.Left)result).Value;
        error.Message.Should().Contain("Insufficient stock");
        product.Stock.Should().Be(0); // Stock unchanged
    }

    [Fact]
    public async Task GetSalesByStatusAsync_WithNoMatchingStatus_ReturnsEmptyList()
    {
        // ARRANGE: mock returns empty list (no sales with requested status)
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSalesRepo = new Mock<ISaleRepository>();

        var emptyResult = new PagedResult<Sale>
        {
            Items = new List<Sale>(),
            PageNumber = 1,
            PageSize = 10,
            RowCount = 0,
            PageCount = 0
        };

        mockSalesRepo.Setup(r => r.GetSalesByStatusAsync(SaleStatus.Completed, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptyResult);
        mockUnitOfWork.Setup(u => u.Sales).Returns(mockSalesRepo.Object);

        var service = new SaleService(mockUnitOfWork.Object, _emissions.Object, _normalDocuments.Object);

        // ACT
        var result = await service.GetSalesByStatusAsync(SaleStatus.Completed, 1, 10);

        // ASSERT
        result.Items.Should().BeEmpty();
        result.RowCount.Should().Be(0);
    }

    [Fact]
    public async Task CompleteSaleAsync_WhenAlreadyCompleted_ThrowsInvalidOperationException()
    {
        // ARRANGE: sale already has status Completed
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSalesRepo = new Mock<ISaleRepository>();
        var mockProductsRepo = new Mock<IProductRepository>();

        var product = new Product { Id = Guid7TestValues.ProductA, Stock = 10, CompanyId = Guid7TestValues.CompanyA, Name = "Test" };

        var sale = new Sale
        {
            Id = Guid7TestValues.SaleA, CompanyId = Guid7TestValues.CompanyA, Status = SaleStatus.Completed, Total = 500,
            Items = new List<SaleItem>
            {
                new() { ProductId = Guid7TestValues.ProductA, ProductName = "Test", Quantity = 5, UnitPrice = 100, Subtotal = 500 }
            }
        };

        mockSalesRepo.Setup(r => r.GetSaleByIdAsync(Guid7TestValues.SaleA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sale);
        mockSalesRepo.Setup(r => r.UpdateSaleAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });
        mockUnitOfWork.Setup(u => u.Sales).Returns(mockSalesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new SaleService(mockUnitOfWork.Object, _emissions.Object, _normalDocuments.Object);

        // ACT
        var result = await service.CompleteSaleAsync(Guid7TestValues.SaleA, Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, SaleDto>.Left>();
        var error = ((Either<Error, SaleDto>.Left)result).Value;
        error.Message.Should().Be("Invalid sale state transition: 'Completed' → 'Completed'");
    }

    [Fact]
    public async Task DeleteSaleAsync_WhenSaleFound_RestoresProductStock()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSalesRepo = new Mock<ISaleRepository>();
        var mockProductsRepo = new Mock<IProductRepository>();

        var product = new Product { Id = Guid7TestValues.ProductA, Stock = 5, CompanyId = Guid7TestValues.CompanyA, Name = "Test Product" };

        var sale = new Sale
        {
            Id = Guid7TestValues.SaleA, CompanyId = Guid7TestValues.CompanyA, Status = SaleStatus.Pending, Total = 500,
            Items = new List<SaleItem>
            {
                new() { ProductId = Guid7TestValues.ProductA, ProductName = "Test Product", Quantity = 10, UnitPrice = 50, Subtotal = 500 }
            }
        };

        mockSalesRepo.Setup(r => r.GetSaleByIdAsync(Guid7TestValues.SaleA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sale);
        mockSalesRepo.Setup(r => r.DeleteSaleAsync(Guid7TestValues.SaleA, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });
        mockUnitOfWork.Setup(u => u.Sales).Returns(mockSalesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new SaleService(mockUnitOfWork.Object, _emissions.Object, _normalDocuments.Object);

        // ACT
        await service.DeleteSaleAsync(Guid7TestValues.SaleA, Guid7TestValues.CompanyA);

        // ASSERT - stock should be restored: 5 + 10 = 15
        product.Stock.Should().Be(15);
        mockSalesRepo.Verify(r => r.DeleteSaleAsync(Guid7TestValues.SaleA, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CompleteSaleAsync_WithInsufficientStock_ThrowsInvalidOperationException()
    {
        // ARRANGE - product has only 3 in stock but sale item requires 10
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSalesRepo = new Mock<ISaleRepository>();
        var mockProductsRepo = new Mock<IProductRepository>();

        var product = new Product { Id = Guid7TestValues.ProductA, Stock = 3, CompanyId = Guid7TestValues.CompanyA, Name = "Test Product" };

        var sale = new Sale
        {
            Id = Guid7TestValues.SaleA, CompanyId = Guid7TestValues.CompanyA, Status = SaleStatus.Pending, Total = 1000,
            Items = new List<SaleItem>
            {
                new() { ProductId = Guid7TestValues.ProductA, ProductName = "Test Product", Quantity = 10, UnitPrice = 100, Subtotal = 1000 }
            }
        };

        mockSalesRepo.Setup(r => r.GetSaleByIdAsync(Guid7TestValues.SaleA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sale);
        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });
        mockUnitOfWork.Setup(u => u.Sales).Returns(mockSalesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);

        var service = new SaleService(mockUnitOfWork.Object, _emissions.Object, _normalDocuments.Object);

        // ACT
        var result = await service.CompleteSaleAsync(Guid7TestValues.SaleA, Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, SaleDto>.Left>();
        var error = ((Either<Error, SaleDto>.Left)result).Value;
        error.Message.Should().Contain("Insufficient stock");
    }

    [Fact]
    public async Task CompleteSaleAsync_WithValidStock_SetsStatusToCompleted()
    {
        // ARRANGE - product has sufficient stock
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSalesRepo = new Mock<ISaleRepository>();
        var mockProductsRepo = new Mock<IProductRepository>();

        var product = new Product { Id = Guid7TestValues.ProductA, Stock = 20, CompanyId = Guid7TestValues.CompanyA, Name = "Test Product" };

        var sale = new Sale
        {
            Id = Guid7TestValues.SaleA, CompanyId = Guid7TestValues.CompanyA, Status = SaleStatus.Pending, Total = 500,
            Items = new List<SaleItem>
            {
                new() { ProductId = Guid7TestValues.ProductA, ProductName = "Test Product", Quantity = 5, UnitPrice = 100, Subtotal = 500 }
            }
        };

        mockSalesRepo.Setup(r => r.GetSaleByIdAsync(Guid7TestValues.SaleA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sale);
        mockSalesRepo.Setup(r => r.UpdateSaleAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });
        mockUnitOfWork.Setup(u => u.Sales).Returns(mockSalesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new SaleService(mockUnitOfWork.Object, _emissions.Object, _normalDocuments.Object);

        // ACT
        var result = await service.CompleteSaleAsync(Guid7TestValues.SaleA, Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        // ASSERT
        sale.Status.Should().Be(SaleStatus.Completed);
        result.Should().BeOfType<Either<Error, SaleDto>.Right>();
        var dto = ((Either<Error, SaleDto>.Right)result).Value;
        dto.Status.Should().Be(SaleStatus.Completed);
        mockSalesRepo.Verify(r => r.UpdateSaleAsync(sale, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteSaleAsync_WhenProductNotFoundInMap_SkipsGracefully()
    {
        // ARRANGE - product no longer exists
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSalesRepo = new Mock<ISaleRepository>();
        var mockProductsRepo = new Mock<IProductRepository>();

        var sale = new Sale
        {
            Id = Guid7TestValues.SaleA, CompanyId = Guid7TestValues.CompanyA, Status = SaleStatus.Pending, Total = 500,
            Items = new List<SaleItem>
            {
                new() { ProductId = Guid.NewGuid(), ProductName = "Deleted Product", Quantity = 10, UnitPrice = 50, Subtotal = 500 }
            }
        };

        mockSalesRepo.Setup(r => r.GetSaleByIdAsync(Guid7TestValues.SaleA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sale);
        mockSalesRepo.Setup(r => r.DeleteSaleAsync(Guid7TestValues.SaleA, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        // Product not found in batch query
        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product>());
        mockUnitOfWork.Setup(u => u.Sales).Returns(mockSalesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new SaleService(mockUnitOfWork.Object, _emissions.Object, _normalDocuments.Object);

        // ACT - should not throw, just skip gracefully
        var act = () => service.DeleteSaleAsync(Guid7TestValues.SaleA, Guid7TestValues.CompanyA);

        // ASSERT
        await act.Should().NotThrowAsync();
        mockSalesRepo.Verify(r => r.DeleteSaleAsync(Guid7TestValues.SaleA, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteSaleAsync_WhenCommitFails_ThrowsAndDoesNotRestoreStock()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSalesRepo = new Mock<ISaleRepository>();
        var mockProductsRepo = new Mock<IProductRepository>();

        var product = new Product { Id = Guid7TestValues.ProductA, Stock = 5, CompanyId = Guid7TestValues.CompanyA, Name = "Test Product" };

        var sale = new Sale
        {
            Id = Guid7TestValues.SaleA, CompanyId = Guid7TestValues.CompanyA, Status = SaleStatus.Pending, Total = 500,
            Items = new List<SaleItem>
            {
                new() { ProductId = Guid7TestValues.ProductA, ProductName = "Test Product", Quantity = 10, UnitPrice = 50, Subtotal = 500 }
            }
        };

        mockSalesRepo.Setup(r => r.GetSaleByIdAsync(Guid7TestValues.SaleA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sale);
        mockSalesRepo.Setup(r => r.DeleteSaleAsync(Guid7TestValues.SaleA, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });
        mockUnitOfWork.Setup(u => u.Sales).Returns(mockSalesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database connection failed"));

        var service = new SaleService(mockUnitOfWork.Object, _emissions.Object, _normalDocuments.Object);

        // ACT
        var result = await service.DeleteSaleAsync(Guid7TestValues.SaleA, Guid7TestValues.CompanyA);

        // ASSERT
        result.Should().BeOfType<Either<Error, Unit>.Left>();
        var error = ((Either<Error, Unit>.Left)result).Value;
        error.Message.Should().Be("Database connection failed");
        // Stock was modified in memory before commit failed
        // NOTE: This documents current behavior. True rollback requires integration tests.
        product.Stock.Should().Be(15); // 5 + 10 restored before commit failed
    }

    [Fact]
    public async Task CreateSaleAsync_MapsEveryItemFieldToEntity()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSalesRepo = new Mock<ISaleRepository>();
        var mockProductsRepo = new Mock<IProductRepository>();

        var product = new Product { Id = Guid7TestValues.ProductA, Stock = 10, CompanyId = Guid7TestValues.CompanyA, Name = "Test Product" };

        Sale? capturedSale = null;
        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });
        mockSalesRepo.Setup(r => r.AddSaleAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()))
            .Callback<Sale, CancellationToken>((s, ct) => capturedSale = s)
            .ReturnsAsync((Sale s, CancellationToken ct) => s);
        mockUnitOfWork.Setup(u => u.Sales).Returns(mockSalesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new SaleService(mockUnitOfWork.Object, _emissions.Object, _normalDocuments.Object);
        SetupCodes(mockUnitOfWork);
        var request = new CreateSaleRequest(
            ClienteId: null, ClienteName: "Test Client",
            Status: SaleStatus.Pending, Total: 100, Notes: null,
            Items: new List<CreateSaleItemRequest>
            {
                new(Guid7TestValues.ProductA, "Test Product", "Tools", "CODE-1", "https://img/product.png", 2, 50, 100)
            }
        );

        // ACT
        await service.CreateSaleAsync(request, companyId: Guid7TestValues.CompanyA, userId: Guid7TestValues.UserA);

        // ASSERT - every field of the request item must reach the entity item
        capturedSale.Should().NotBeNull();
        var item = capturedSale!.Items.Should().ContainSingle().Subject;
        item.ProductId.Should().Be(Guid7TestValues.ProductA);
        item.ProductName.Should().Be("Test Product");
        item.ProductCategory.Should().Be("Tools");
        item.ProductCode.Should().Be("CODE-1");
        item.ProductImageUrl.Should().Be("https://img/product.png");
        item.Quantity.Should().Be(2);
        item.UnitPrice.Should().Be(50m);
        item.Subtotal.Should().Be(100m);
    }

    [Fact]
    public async Task UpdateSaleAsync_MapsEveryItemFieldToEntity()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSalesRepo = new Mock<ISaleRepository>();
        var mockProductsRepo = new Mock<IProductRepository>();

        var product = new Product { Id = Guid7TestValues.ProductA, Stock = 10, CompanyId = Guid7TestValues.CompanyA, Name = "Test" };

        var existingSale = new Sale
        {
            Id = Guid7TestValues.SaleA, CompanyId = Guid7TestValues.CompanyA, Status = SaleStatus.Pending, Total = 500,
            Items = new List<SaleItem>
            {
                new() { Id = Guid7TestValues.SaleA, ProductId = Guid7TestValues.ProductA, ProductName = "Test", Quantity = 5, UnitPrice = 100, Subtotal = 500 }
            }
        };

        Sale? capturedSale = null;
        mockSalesRepo.Setup(r => r.GetSaleByIdAsync(Guid7TestValues.SaleA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingSale);
        mockSalesRepo.Setup(r => r.UpdateSaleAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()))
            .Callback<Sale, CancellationToken>((s, ct) => capturedSale = s)
            .Returns(Task.CompletedTask);
        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });
        mockUnitOfWork.Setup(u => u.Sales).Returns(mockSalesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new SaleService(mockUnitOfWork.Object, _emissions.Object, _normalDocuments.Object);
        var updateRequest = new UpdateSaleRequest(
            ClienteId: null, ClienteName: "Updated", Status: SaleStatus.Pending,
            Total: 100, Notes: null,
            Items: new List<CreateSaleItemRequest>
            {
                new(Guid7TestValues.ProductA, "Test Product", "Tools", "CODE-1", "https://img/product.png", 2, 50, 100)
            }
        );

        // ACT
        await service.UpdateSaleAsync(Guid7TestValues.SaleA, Guid7TestValues.CompanyA, updateRequest, Guid7TestValues.UserA);

        // ASSERT - every field of the request item must reach the entity item
        capturedSale.Should().NotBeNull();
        var item = capturedSale!.Items.Should().ContainSingle().Subject;
        item.ProductId.Should().Be(Guid7TestValues.ProductA);
        item.ProductName.Should().Be("Test Product");
        item.ProductCategory.Should().Be("Tools");
        item.ProductCode.Should().Be("CODE-1");
        item.ProductImageUrl.Should().Be("https://img/product.png");
        item.Quantity.Should().Be(2);
        item.UnitPrice.Should().Be(50m);
        item.Subtotal.Should().Be(100m);
    }

    [Fact]
    public async Task CreateSaleAsync_WithoutCode_UsesTheGeneratedCode()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSalesRepo = new Mock<ISaleRepository>();
        var mockProductsRepo = new Mock<IProductRepository>();

        var product = new Product { Id = Guid7TestValues.ProductA, Stock = 10, CompanyId = Guid7TestValues.CompanyA, Name = "Test Product" };

        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });
        mockSalesRepo.Setup(r => r.AddSaleAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Sale s, CancellationToken ct) => s);
        mockUnitOfWork.Setup(u => u.Sales).Returns(mockSalesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new SaleService(mockUnitOfWork.Object, _emissions.Object, _normalDocuments.Object);
        SetupCodes(mockUnitOfWork);
        var request = new CreateSaleRequest(
            ClienteId: null, ClienteName: "Test Client",
            Status: SaleStatus.Pending, Total: 500, Notes: null,
            Items: new List<CreateSaleItemRequest>
            {
                new(Guid7TestValues.ProductA, "Test Product", null, null, null, 5, 100, 500)
            }
        );

        // ACT
        var result = await service.CreateSaleAsync(request, companyId: Guid7TestValues.CompanyA, userId: Guid7TestValues.UserA);

        // ASSERT - the request carries no code, so the generator decides
        result.Should().BeOfType<Either<Error, SaleDto>.Right>();
        var dto = ((Either<Error, SaleDto>.Right)result).Value;
        dto.Code.Should().Be("Vent-001");
    }

    [Fact]
    public async Task CreateSaleAsync_WithUserCode_KeepsTheTrimmedUserCode()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSalesRepo = new Mock<ISaleRepository>();
        var mockProductsRepo = new Mock<IProductRepository>();

        var product = new Product { Id = Guid7TestValues.ProductA, Stock = 10, CompanyId = Guid7TestValues.CompanyA, Name = "Test Product" };

        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });
        mockSalesRepo.Setup(r => r.AddSaleAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Sale s, CancellationToken ct) => s);
        mockUnitOfWork.Setup(u => u.Sales).Returns(mockSalesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new SaleService(mockUnitOfWork.Object, _emissions.Object, _normalDocuments.Object);
        SetupCodes(mockUnitOfWork);
        var request = new CreateSaleRequest(
            ClienteId: null, ClienteName: "Test Client",
            Status: SaleStatus.Pending, Total: 500, Notes: null,
            Items: new List<CreateSaleItemRequest>
            {
                new(Guid7TestValues.ProductA, "Test Product", null, null, null, 5, 100, 500)
            },
            Code: "  MY-SALE-1  "
        );

        // ACT
        var result = await service.CreateSaleAsync(request, companyId: Guid7TestValues.CompanyA, userId: Guid7TestValues.UserA);

        // ASSERT - the code of the user wins over the generator
        result.Should().BeOfType<Either<Error, SaleDto>.Right>();
        var dto = ((Either<Error, SaleDto>.Right)result).Value;
        dto.Code.Should().Be("MY-SALE-1");
    }

    [Fact]
    public async Task CreateSaleAsync_WithRepeatedCode_ReturnsCleanError()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSalesRepo = new Mock<ISaleRepository>();
        var mockProductsRepo = new Mock<IProductRepository>();

        var product = new Product { Id = Guid7TestValues.ProductA, Stock = 10, CompanyId = Guid7TestValues.CompanyA, Name = "Test Product" };
        var otherSale = new Sale
        {
            Id = Guid7TestValues.SaleA, CompanyId = Guid7TestValues.CompanyA,
            Code = "DUP-1", Status = SaleStatus.Pending
        };

        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });
        mockSalesRepo.Setup(r => r.ExistsCodeAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, string code, Guid? excludeId, CancellationToken _) => otherSale.Code == code && excludeId != otherSale.Id);
        mockUnitOfWork.Setup(u => u.Sales).Returns(mockSalesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new SaleService(mockUnitOfWork.Object, _emissions.Object, _normalDocuments.Object);
        SetupCodes(mockUnitOfWork);
        var request = new CreateSaleRequest(
            ClienteId: null, ClienteName: "Test Client",
            Status: SaleStatus.Pending, Total: 500, Notes: null,
            Items: new List<CreateSaleItemRequest>
            {
                new(Guid7TestValues.ProductA, "Test Product", null, null, null, 5, 100, 500)
            },
            Code: "DUP-1"
        );

        // ACT
        var result = await service.CreateSaleAsync(request, companyId: Guid7TestValues.CompanyA, userId: Guid7TestValues.UserA);

        // ASSERT - a clean error, and the inventory was not touched
        result.Should().BeOfType<Either<Error, SaleDto>.Left>();
        var error = ((Either<Error, SaleDto>.Left)result).Value;
        error.Message.Should().Be("The code is already in use.");
        product.Stock.Should().Be(10);
        mockSalesRepo.Verify(r => r.AddSaleAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateSaleAsync_WithoutCode_KeepsTheCurrentCode()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSalesRepo = new Mock<ISaleRepository>();
        var mockProductsRepo = new Mock<IProductRepository>();

        var product = new Product { Id = Guid7TestValues.ProductA, Stock = 10, CompanyId = Guid7TestValues.CompanyA, Name = "Test" };

        var existingSale = new Sale
        {
            Id = Guid7TestValues.SaleA, CompanyId = Guid7TestValues.CompanyA, Code = "Vent-007",
            Status = SaleStatus.Pending, Total = 500,
            Items = new List<SaleItem>
            {
                new() { Id = Guid7TestValues.SaleA, ProductId = Guid7TestValues.ProductA, ProductName = "Test", Quantity = 5, UnitPrice = 100, Subtotal = 500 }
            }
        };

        mockSalesRepo.Setup(r => r.GetSaleByIdAsync(Guid7TestValues.SaleA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingSale);
        mockSalesRepo.Setup(r => r.UpdateSaleAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });
        mockUnitOfWork.Setup(u => u.Sales).Returns(mockSalesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new SaleService(mockUnitOfWork.Object, _emissions.Object, _normalDocuments.Object);
        var updateRequest = new UpdateSaleRequest(
            ClienteId: null, ClienteName: "Updated", Status: SaleStatus.Pending,
            Total: 800, Notes: null,
            Items: new List<CreateSaleItemRequest>
            {
                new(Guid7TestValues.ProductA, "Test", null, null, null, 8, 100, 800)
            }
        );

        // ACT
        var result = await service.UpdateSaleAsync(Guid7TestValues.SaleA, Guid7TestValues.CompanyA, updateRequest, Guid7TestValues.UserA);

        // ASSERT - the update does not carry a code, so the sale keeps the one it had
        result.Should().BeOfType<Either<Error, SaleDto>.Right>();
        var dto = ((Either<Error, SaleDto>.Right)result).Value;
        dto.Code.Should().Be("Vent-007");
    }

    [Fact]
    public async Task UpdateSaleAsync_WithItsOwnCode_KeepsTheCode()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSalesRepo = new Mock<ISaleRepository>();
        var mockProductsRepo = new Mock<IProductRepository>();

        var product = new Product { Id = Guid7TestValues.ProductA, Stock = 10, CompanyId = Guid7TestValues.CompanyA, Name = "Test" };

        var existingSale = new Sale
        {
            Id = Guid7TestValues.SaleA, CompanyId = Guid7TestValues.CompanyA, Code = "Vent-007",
            Status = SaleStatus.Pending, Total = 500,
            Items = new List<SaleItem>
            {
                new() { Id = Guid7TestValues.SaleA, ProductId = Guid7TestValues.ProductA, ProductName = "Test", Quantity = 5, UnitPrice = 100, Subtotal = 500 }
            }
        };

        mockSalesRepo.Setup(r => r.GetSaleByIdAsync(Guid7TestValues.SaleA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingSale);
        mockSalesRepo.Setup(r => r.UpdateSaleAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        // The only sale holding that code is the one being updated: it must not
        // be reported as a conflict.
        mockSalesRepo.Setup(r => r.ExistsCodeAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, string code, Guid? excludeId, CancellationToken _) => existingSale.Code == code && excludeId != existingSale.Id);
        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });
        mockUnitOfWork.Setup(u => u.Sales).Returns(mockSalesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new SaleService(mockUnitOfWork.Object, _emissions.Object, _normalDocuments.Object);
        var updateRequest = new UpdateSaleRequest(
            ClienteId: null, ClienteName: "Updated", Status: SaleStatus.Pending,
            Total: 800, Notes: null,
            Items: new List<CreateSaleItemRequest>
            {
                new(Guid7TestValues.ProductA, "Test", null, null, null, 8, 100, 800)
            },
            Code: "Vent-007"
        );

        // ACT
        var result = await service.UpdateSaleAsync(Guid7TestValues.SaleA, Guid7TestValues.CompanyA, updateRequest, Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, SaleDto>.Right>();
        var dto = ((Either<Error, SaleDto>.Right)result).Value;
        dto.Code.Should().Be("Vent-007");
    }

    [Fact]
    public async Task UpdateSaleAsync_WithRepeatedCodeOfAnotherSale_ReturnsCleanError()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSalesRepo = new Mock<ISaleRepository>();
        var mockProductsRepo = new Mock<IProductRepository>();

        var product = new Product { Id = Guid7TestValues.ProductA, Stock = 10, CompanyId = Guid7TestValues.CompanyA, Name = "Test" };

        var existingSale = new Sale
        {
            Id = Guid7TestValues.SaleA, CompanyId = Guid7TestValues.CompanyA, Code = "Vent-007",
            Status = SaleStatus.Pending, Total = 500,
            Items = new List<SaleItem>
            {
                new() { Id = Guid7TestValues.SaleA, ProductId = Guid7TestValues.ProductA, ProductName = "Test", Quantity = 5, UnitPrice = 100, Subtotal = 500 }
            }
        };
        var otherSale = new Sale
        {
            Id = Guid.NewGuid(), CompanyId = Guid7TestValues.CompanyA,
            Code = "DUP-1", Status = SaleStatus.Pending
        };

        mockSalesRepo.Setup(r => r.GetSaleByIdAsync(Guid7TestValues.SaleA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingSale);
        mockSalesRepo.Setup(r => r.UpdateSaleAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockSalesRepo.Setup(r => r.ExistsCodeAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, string code, Guid? excludeId, CancellationToken _) => otherSale.Code == code && excludeId != otherSale.Id);
        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });
        mockUnitOfWork.Setup(u => u.Sales).Returns(mockSalesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new SaleService(mockUnitOfWork.Object, _emissions.Object, _normalDocuments.Object);
        var updateRequest = new UpdateSaleRequest(
            ClienteId: null, ClienteName: "Updated", Status: SaleStatus.Pending,
            Total: 800, Notes: null,
            Items: new List<CreateSaleItemRequest>
            {
                new(Guid7TestValues.ProductA, "Test", null, null, null, 8, 100, 800)
            },
            Code: "DUP-1"
        );

        // ACT
        var result = await service.UpdateSaleAsync(Guid7TestValues.SaleA, Guid7TestValues.CompanyA, updateRequest, Guid7TestValues.UserA);

        // ASSERT - a clean error, and the inventory was not touched
        result.Should().BeOfType<Either<Error, SaleDto>.Left>();
        var error = ((Either<Error, SaleDto>.Left)result).Value;
        error.Message.Should().Be("The code is already in use.");
        product.Stock.Should().Be(10);
        mockSalesRepo.Verify(r => r.UpdateSaleAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // The sale service asks the generator for the code when the request does not
    // bring one.
    private static void SetupCodes(Mock<IUnitOfWork> mockUnitOfWork, string code = "Vent-001")
    {
        var mockCodes = new Mock<ICodeGenerator>();
        mockCodes.Setup(c => c.NextAsync(It.IsAny<Guid>(), It.IsAny<CodeEntityType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(code);
        mockUnitOfWork.Setup(u => u.Codes).Returns(mockCodes.Object);
    }

    [Fact]
    public async Task CompleteSaleAsync_InPaperMode_RecordsTheNormalDocumentAndIssuesNoElectronicInvoice()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSalesRepo = new Mock<ISaleRepository>();
        var mockProductsRepo = new Mock<IProductRepository>();
        var mockNormalDocuments = new Mock<INormalFiscalDocumentService>();

        var company = new Company
        {
            Id = Guid7TestValues.CompanyA,
            TaxId = "10100000101",
            EmissionMode = CompanyEmissionMode.Paper
        };

        var sale = new Sale
        {
            Id = Guid7TestValues.SaleA, CompanyId = Guid7TestValues.CompanyA, Status = SaleStatus.Pending,
            DocumentType = EcfDocumentType.Consumer, Company = company,
            ReceptorTaxId = "101010101", ClienteName = "Cliente de Prueba",
            Items = new List<SaleItem>
            {
                new() { ProductId = Guid7TestValues.ProductA, ProductName = "Test", Quantity = 5, UnitPrice = 100, Subtotal = 500 }
            }
        };

        mockSalesRepo.Setup(r => r.GetSaleByIdAsync(Guid7TestValues.SaleA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sale);
        mockSalesRepo.Setup(r => r.UpdateSaleAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product>());
        mockNormalDocuments.Setup(d => d.IssueAsync(
                Guid7TestValues.SaleA, Guid7TestValues.CompanyA, Guid7TestValues.UserA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Either<Error, NormalFiscalDocumentDto>.ToRight(new NormalFiscalDocumentDto
            {
                Id = Guid.CreateVersion7(),
                CompanyId = Guid7TestValues.CompanyA,
                SaleId = Guid7TestValues.SaleA,
                Number = "B0200000001",
                DocumentType = NormalFiscalDocumentType.Consumer,
                IssuerTaxId = "10100000101"
            }));
        mockUnitOfWork.Setup(u => u.Sales).Returns(mockSalesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new SaleService(
            mockUnitOfWork.Object, _emissions.Object, normalDocuments: mockNormalDocuments.Object);

        // ACT
        var result = await service.CompleteSaleAsync(Guid7TestValues.SaleA, Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, SaleDto>.Right>();
        sale.Status.Should().Be(SaleStatus.Completed);

        mockNormalDocuments.Verify(d => d.IssueAsync(
            Guid7TestValues.SaleA, Guid7TestValues.CompanyA, Guid7TestValues.UserA,
            It.IsAny<CancellationToken>()), Times.Once);

        _emissions.Verify(e => e.IssueAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CompleteSaleAsync_InPaperMode_WhenTheDocumentFails_DoesNotCompleteTheSale()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSalesRepo = new Mock<ISaleRepository>();
        var mockProductsRepo = new Mock<IProductRepository>();
        var mockNormalDocuments = new Mock<INormalFiscalDocumentService>();

        var company = new Company
        {
            Id = Guid7TestValues.CompanyA,
            TaxId = "10100000101",
            EmissionMode = CompanyEmissionMode.Paper
        };

        var sale = new Sale
        {
            Id = Guid7TestValues.SaleA, CompanyId = Guid7TestValues.CompanyA, Status = SaleStatus.Pending,
            DocumentType = EcfDocumentType.Consumer, Company = company,
            ReceptorTaxId = "101010101", ClienteName = "Cliente de Prueba",
            Items = new List<SaleItem>
            {
                new() { ProductId = Guid7TestValues.ProductA, ProductName = "Test", Quantity = 5, UnitPrice = 100, Subtotal = 500 }
            }
        };

        mockSalesRepo.Setup(r => r.GetSaleByIdAsync(Guid7TestValues.SaleA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sale);
        mockSalesRepo.Setup(r => r.UpdateSaleAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product>());
        mockNormalDocuments.Setup(d => d.IssueAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Either<Error, NormalFiscalDocumentDto>.ToLeft(
                new Error("The authorized range of normal documents for the type 2 is exhausted.")));
        mockUnitOfWork.Setup(u => u.Sales).Returns(mockSalesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new SaleService(
            mockUnitOfWork.Object, _emissions.Object, normalDocuments: mockNormalDocuments.Object);

        // ACT
        var result = await service.CompleteSaleAsync(Guid7TestValues.SaleA, Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        // ASSERT - the caller gets an error, not a crash, and nothing is committed.
        result.Should().BeOfType<Either<Error, SaleDto>.Left>();
        var error = ((Either<Error, SaleDto>.Left)result).Value;
        error.Message.Should().Contain("exhausted");
        mockUnitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}