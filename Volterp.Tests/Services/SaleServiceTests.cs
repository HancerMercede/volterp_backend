using FluentAssertions;
using Moq;
using Volterp.Application.DTOs;
using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Application.Services;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Xunit;

namespace Volterp.Tests.Services;

public class SaleServiceTests
{
    [Fact]
    public async Task CreateSaleAsync_WithValidInput_DecrementsProductStock()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSalesRepo = new Mock<ISaleRepository>();
        var mockProductsRepo = new Mock<IProductRepository>();

        var product = new Product { Id = 1, Stock = 10, CompanyId = 1, Name = "Test Product" };

        // Batch query: GetProductsByIdsAsync instead of individual GetProductByIdAsync
        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });
        mockSalesRepo.Setup(r => r.AddSaleAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Sale s, CancellationToken ct) => s);
        mockUnitOfWork.Setup(u => u.Sales).Returns(mockSalesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new SaleService(mockUnitOfWork.Object);
        var request = new CreateSaleRequest(
            CompanyId: 1, ClienteId: null, ClienteName: "Test Client",
            Status: SaleStatus.Pending, Total: 500, Notes: null,
            Items: new List<CreateSaleItemRequest>
            {
                new(1, "Test Product", null, null, null, 5, 100, 500)
            }
        );

        // ACT
        await service.CreateSaleAsync(request);

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

        var product = new Product { Id = 1, Stock = 20, CompanyId = 1, Name = "Test Product" };

        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });
        mockSalesRepo.Setup(r => r.AddSaleAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Sale s, CancellationToken ct) => s);
        mockUnitOfWork.Setup(u => u.Sales).Returns(mockSalesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new SaleService(mockUnitOfWork.Object);
        var request = new CreateSaleRequest(
            CompanyId: 1, ClienteId: null, ClienteName: "Test",
            Status: SaleStatus.Pending, Total: 1500, Notes: null,
            Items: new List<CreateSaleItemRequest>
            {
                new(1, "Test", null, null, null, 15, 100, 1500)
            }
        );

        // ACT
        await service.CreateSaleAsync(request);

        // ASSERT
        product.Stock.Should().Be(5);
    }

    [Fact]
    public async Task CompleteSaleAsync_WithValidInput_SetsStatusToCompleted()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSalesRepo = new Mock<ISaleRepository>();

        var sale = new Sale
        {
            Id = 1, CompanyId = 1, Status = SaleStatus.Pending, Total = 500,
            Items = new List<SaleItem>
            {
                new() { ProductId = 1, ProductName = "Test", Quantity = 5, UnitPrice = 100, Subtotal = 500 }
            }
        };

        mockSalesRepo.Setup(r => r.GetSaleByIdAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sale);
        mockSalesRepo.Setup(r => r.UpdateSaleAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockUnitOfWork.Setup(u => u.Sales).Returns(mockSalesRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new SaleService(mockUnitOfWork.Object);

        // ACT
        var result = await service.CompleteSaleAsync(1, 1);

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

        var product = new Product { Id = 1, Stock = 10, CompanyId = 1, Name = "Test" };

        var existingSale = new Sale
        {
            Id = 1, CompanyId = 1, Status = SaleStatus.Pending, Total = 500,
            Items = new List<SaleItem>
            {
                new() { Id = 1, ProductId = 1, ProductName = "Test", Quantity = 5, UnitPrice = 100, Subtotal = 500 }
            }
        };

        mockSalesRepo.Setup(r => r.GetSaleByIdAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingSale);
        mockSalesRepo.Setup(r => r.UpdateSaleAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        // Batch query: GetProductsByIdsAsync returns products for both old and new items
        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });
        mockUnitOfWork.Setup(u => u.Sales).Returns(mockSalesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new SaleService(mockUnitOfWork.Object);
        var updateRequest = new UpdateSaleRequest(
            ClienteId: null, ClienteName: "Updated", Status: SaleStatus.Pending,
            Total: 800, Notes: null,
            Items: new List<CreateSaleItemRequest>
            {
                new(1, "Test", null, null, null, 8, 100, 800) // Changed from 5 to 8
            }
        );

        // ACT
        await service.UpdateSaleAsync(1, 1, updateRequest);

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
                new() { Id = 1, CompanyId = 1, Status = SaleStatus.Pending, Total = 500, ClienteName = "Client A" },
                new() { Id = 2, CompanyId = 1, Status = SaleStatus.Completed, Total = 300, ClienteName = "Client B" }
            },
            PageNumber = 1,
            PageSize = 10,
            RowCount = 2,
            PageCount = 1
        };

        mockSalesRepo.Setup(r => r.GetAllSalesByCompanyAsync(1, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sales);
        mockUnitOfWork.Setup(u => u.Sales).Returns(mockSalesRepo.Object);

        var service = new SaleService(mockUnitOfWork.Object);

        // ACT
        var result = await service.GetAllSalesAsync(1, 1, 10);

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
                new() { Id = 1, CompanyId = 1, Status = SaleStatus.Pending, Total = 500, ClienteName = "Client A" }
            },
            PageNumber = 1,
            PageSize = 10,
            RowCount = 1,
            PageCount = 1
        };

        mockSalesRepo.Setup(r => r.GetSalesByStatusAsync(1, SaleStatus.Pending, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sales);
        mockUnitOfWork.Setup(u => u.Sales).Returns(mockSalesRepo.Object);

        var service = new SaleService(mockUnitOfWork.Object);

        // ACT
        var result = await service.GetSalesByStatusAsync(1, SaleStatus.Pending, 1, 10);

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
            Id = 1, CompanyId = 1, Status = SaleStatus.Pending, Total = 500,
            ClienteName = "Test Client",
            Items = new List<SaleItem>
            {
                new() { Id = 1, ProductId = 1, ProductName = "Test", Quantity = 5, UnitPrice = 100, Subtotal = 500 }
            }
        };

        mockSalesRepo.Setup(r => r.GetSaleByIdAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sale);
        mockUnitOfWork.Setup(u => u.Sales).Returns(mockSalesRepo.Object);

        var service = new SaleService(mockUnitOfWork.Object);

        // ACT
        var result = await service.GetSaleByIdAsync(1, 1);

        // ASSERT
        result.Should().NotBeNull();
        result!.Id.Should().Be(1);
        result.ClienteName.Should().Be("Test Client");
    }

    [Fact]
    public async Task GetSaleByIdAsync_WhenNotFound_ReturnsNull()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSalesRepo = new Mock<ISaleRepository>();

        mockSalesRepo.Setup(r => r.GetSaleByIdAsync(999, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Sale?)null);
        mockUnitOfWork.Setup(u => u.Sales).Returns(mockSalesRepo.Object);

        var service = new SaleService(mockUnitOfWork.Object);

        // ACT
        var result = await service.GetSaleByIdAsync(999, 1);

        // ASSERT
        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateSaleAsync_WithProductNotFound_DoesNotModifyStock()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSalesRepo = new Mock<ISaleRepository>();
        var mockProductsRepo = new Mock<IProductRepository>();

        // Product not found in batch query
        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product>());
        mockSalesRepo.Setup(r => r.AddSaleAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Sale s, CancellationToken ct) => s);
        mockUnitOfWork.Setup(u => u.Sales).Returns(mockSalesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new SaleService(mockUnitOfWork.Object);
        var request = new CreateSaleRequest(
            CompanyId: 1, ClienteId: null, ClienteName: "Test",
            Status: SaleStatus.Pending, Total: 500, Notes: null,
            Items: new List<CreateSaleItemRequest>
            {
                new(999, "NonExistent", null, null, null, 5, 100, 500) // product doesn't exist
            }
        );

        // ACT - should not throw, just doesn't decrement stock for missing products
        var result = await service.CreateSaleAsync(request);

        // ASSERT - sale created, no stock modification occurred
        result.Should().NotBeNull();
        mockSalesRepo.Verify(r => r.AddSaleAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateSaleAsync_WhenSaleNotFound_ThrowsArgumentException()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSalesRepo = new Mock<ISaleRepository>();

        mockSalesRepo.Setup(r => r.GetSaleByIdAsync(999, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Sale?)null);
        mockUnitOfWork.Setup(u => u.Sales).Returns(mockSalesRepo.Object);

        var service = new SaleService(mockUnitOfWork.Object);
        var updateRequest = new UpdateSaleRequest(
            ClienteId: null, ClienteName: "Updated", Status: SaleStatus.Pending,
            Total: 800, Notes: null,
            Items: new List<CreateSaleItemRequest>
            {
                new(1, "Test", null, null, null, 5, 100, 500)
            }
        );

        // ACT
        var act = () => service.UpdateSaleAsync(999, 1, updateRequest);

        // ASSERT
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("Sale not found");
    }

    [Fact]
    public async Task CompleteSaleAsync_WhenSaleNotFound_ThrowsArgumentException()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSalesRepo = new Mock<ISaleRepository>();

        mockSalesRepo.Setup(r => r.GetSaleByIdAsync(999, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Sale?)null);
        mockUnitOfWork.Setup(u => u.Sales).Returns(mockSalesRepo.Object);

        var service = new SaleService(mockUnitOfWork.Object);

        // ACT
        var act = () => service.CompleteSaleAsync(999, 1);

        // ASSERT
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("Sale not found");
    }

    [Fact]
    public async Task DeleteSaleAsync_WhenSaleNotFound_ThrowsArgumentException()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSalesRepo = new Mock<ISaleRepository>();

        mockSalesRepo.Setup(r => r.GetSaleByIdAsync(999, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Sale?)null);
        mockUnitOfWork.Setup(u => u.Sales).Returns(mockSalesRepo.Object);

        var service = new SaleService(mockUnitOfWork.Object);

        // ACT
        var act = () => service.DeleteSaleAsync(999, 1);

        // ASSERT
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("Sale not found");
    }

    [Fact]
    public async Task DeleteSaleAsync_WhenSaleFound_CallsDelete()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSalesRepo = new Mock<ISaleRepository>();

        var sale = new Sale
        {
            Id = 1, CompanyId = 1, Status = SaleStatus.Pending, Total = 500,
            Items = new List<SaleItem>()
        };

        mockSalesRepo.Setup(r => r.GetSaleByIdAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sale);
        mockSalesRepo.Setup(r => r.DeleteSaleAsync(1, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockUnitOfWork.Setup(u => u.Sales).Returns(mockSalesRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new SaleService(mockUnitOfWork.Object);

        // ACT
        await service.DeleteSaleAsync(1, 1);

        // ASSERT
        mockSalesRepo.Verify(r => r.DeleteSaleAsync(1, It.IsAny<CancellationToken>()), Times.Once);
    }
}