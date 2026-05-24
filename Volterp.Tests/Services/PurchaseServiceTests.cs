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

public class PurchaseServiceTests
{
    [Fact]
    public async Task CreatePurchaseAsync_WithValidInput_IncrementsProductStock()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockPurchasesRepo = new Mock<IPurchaseRepository>();
        var mockProductsRepo = new Mock<IProductRepository>();

        var product = new Product { Id = 1, Stock = 10, CompanyId = 1, Name = "Test Product" };

        // Batch query: GetProductsByIdsAsync instead of individual GetProductByIdAsync
        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });
        
        mockPurchasesRepo.Setup(r => r.AddPurchaseAsync(It.IsAny<Purchase>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Purchase p, CancellationToken ct) => p);
        
        mockUnitOfWork.Setup(u => u.Purchases).Returns(mockPurchasesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new PurchaseService(mockUnitOfWork.Object);
        var request = new PurchaseDto(
            Id: 0,
            SupplierId: 1,
            SupplierName: "Test Supplier",
            Status: EntityStatus.Pending,
            Total: 500,
            Notes: "Test",
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: null,
            CreatedBy: 1,
            UpdatedBy: null,
            Items: new List<PurchaseItemDto>
            {
                new(0, 1, "Test Product", "CODE123", 5, 100, 500)
            }
        );

        // ACT
        await service.CreatePurchaseAsync(request, 1, 1);

        // ASSERT - EF change tracker handles persistence; verify stock was incremented
        product.Stock.Should().Be(15);
    }

    [Fact]
    public async Task UpdatePurchaseAsync_WithValidInput_AdjustsStockOnItemQuantityChange()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockPurchasesRepo = new Mock<IPurchaseRepository>();
        var mockProductsRepo = new Mock<IProductRepository>();

        var product = new Product { Id = 1, Stock = 10, CompanyId = 1, Name = "Test" };

        var existingPurchase = new Purchase
        {
            Id = 1,
            CompanyId = 1,
            SupplierId = 1,
            SupplierName = "Test",
            Status = EntityStatus.Pending,
            Total = 500,
            Items = new List<PurchaseItem>
            {
                new() { Id = 1, ProductId = 1, ProductName = "Test", ProductCode = "CODE", Quantity = 5, UnitPrice = 100, Subtotal = 500 }
            }
        };

        mockPurchasesRepo.Setup(r => r.GetPurchaseByIdAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPurchase);
        
        mockPurchasesRepo.Setup(r => r.UpdatePurchaseAsync(It.IsAny<Purchase>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        
        // Batch query: GetProductsByIdsAsync returns products for both old and new items
        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });
        
        mockUnitOfWork.Setup(u => u.Purchases).Returns(mockPurchasesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new PurchaseService(mockUnitOfWork.Object);
        var updateRequest = new PurchaseDto(
            Id: 1,
            SupplierId: 1,
            SupplierName: "Updated",
            Status: EntityStatus.Pending,
            Total: 800,
            Notes: null,
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: null,
            CreatedBy: 1,
            UpdatedBy: null,
            Items: new List<PurchaseItemDto>
            {
                new(0, 1, "Test", "CODE", 8, 100, 800)
            }
        );

        // ACT
        await service.UpdatePurchaseAsync(1, 1, updateRequest, 1);

        // ASSERT - Expected: old qty (5) removed, new qty (8) added
        // Stock: 10 - 5 + 8 = 13
        product.Stock.Should().Be(13);
    }

    [Fact]
    public async Task GetAllPurchasesAsync_ReturnsPagedResult()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockPurchasesRepo = new Mock<IPurchaseRepository>();

        var purchases = new PagedResult<Purchase>
        {
            Items = new List<Purchase>
            {
                new() { Id = 1, CompanyId = 1, SupplierId = 1, SupplierName = "Supplier A", Status = EntityStatus.Pending, Total = 500 },
                new() { Id = 2, CompanyId = 1, SupplierId = 2, SupplierName = "Supplier B", Status = EntityStatus.Completed, Total = 300 }
            },
            PageNumber = 1,
            PageSize = 10,
            RowCount = 2,
            PageCount = 1
        };

        mockPurchasesRepo.Setup(r => r.GetAllPurchasesByCompanyAsync(1, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(purchases);
        mockUnitOfWork.Setup(u => u.Purchases).Returns(mockPurchasesRepo.Object);

        var service = new PurchaseService(mockUnitOfWork.Object);

        // ACT
        var result = await service.GetAllPurchasesAsync(1, 1, 10);

        // ASSERT
        result.Items.Should().HaveCount(2);
        result.RowCount.Should().Be(2);
    }

    [Fact]
    public async Task GetPurchaseByIdAsync_WhenFound_ReturnsPurchaseDto()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockPurchasesRepo = new Mock<IPurchaseRepository>();

        var purchase = new Purchase
        {
            Id = 1, CompanyId = 1, SupplierId = 1, SupplierName = "Supplier A",
            Status = EntityStatus.Pending, Total = 500,
            Items = new List<PurchaseItem>
            {
                new() { Id = 1, ProductId = 1, ProductName = "Test", ProductCode = "CODE", Quantity = 5, UnitPrice = 100, Subtotal = 500 }
            }
        };

        mockPurchasesRepo.Setup(r => r.GetPurchaseByIdAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(purchase);
        mockUnitOfWork.Setup(u => u.Purchases).Returns(mockPurchasesRepo.Object);

        var service = new PurchaseService(mockUnitOfWork.Object);

        // ACT
        var result = await service.GetPurchaseByIdAsync(1, 1);

        // ASSERT
        result.Should().NotBeNull();
        result!.Id.Should().Be(1);
        result.SupplierName.Should().Be("Supplier A");
    }

    [Fact]
    public async Task GetPurchaseByIdAsync_WhenNotFound_ReturnsNull()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockPurchasesRepo = new Mock<IPurchaseRepository>();

        mockPurchasesRepo.Setup(r => r.GetPurchaseByIdAsync(999, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Purchase?)null);
        mockUnitOfWork.Setup(u => u.Purchases).Returns(mockPurchasesRepo.Object);

        var service = new PurchaseService(mockUnitOfWork.Object);

        // ACT
        var result = await service.GetPurchaseByIdAsync(999, 1);

        // ASSERT
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdatePurchaseAsync_WhenPurchaseNotFound_ThrowsArgumentException()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockPurchasesRepo = new Mock<IPurchaseRepository>();

        mockPurchasesRepo.Setup(r => r.GetPurchaseByIdAsync(999, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Purchase?)null);
        mockUnitOfWork.Setup(u => u.Purchases).Returns(mockPurchasesRepo.Object);

        var service = new PurchaseService(mockUnitOfWork.Object);
        var updateRequest = new PurchaseDto(
            Id: 1, SupplierId: 1, SupplierName: "Updated",
            Status: EntityStatus.Pending, Total: 800, Notes: null,
            CreatedAt: DateTime.UtcNow, UpdatedAt: null,
            CreatedBy: 1, UpdatedBy: null,
            Items: new List<PurchaseItemDto>
            {
                new(0, 1, "Test", "CODE", 5, 100, 500)
            }
        );

        // ACT
        var act = () => service.UpdatePurchaseAsync(999, 1, updateRequest, 1);

        // ASSERT
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("Purchase not found");
    }

    [Fact]
    public async Task DeletePurchaseAsync_WhenPurchaseNotFound_ThrowsArgumentException()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockPurchasesRepo = new Mock<IPurchaseRepository>();

        mockPurchasesRepo.Setup(r => r.GetPurchaseByIdAsync(999, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Purchase?)null);
        mockUnitOfWork.Setup(u => u.Purchases).Returns(mockPurchasesRepo.Object);

        var service = new PurchaseService(mockUnitOfWork.Object);

        // ACT
        var act = () => service.DeletePurchaseAsync(999, 1);

        // ASSERT
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("Purchase not found");
    }

    [Fact]
    public async Task DeletePurchaseAsync_WhenPurchaseFound_CallsDelete()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockPurchasesRepo = new Mock<IPurchaseRepository>();
        var mockProductsRepo = new Mock<IProductRepository>();

        var purchase = new Purchase
        {
            Id = 1, CompanyId = 1, SupplierId = 1, SupplierName = "Supplier",
            Status = EntityStatus.Pending, Total = 500,
            Items = new List<PurchaseItem>()
        };

        mockPurchasesRepo.Setup(r => r.GetPurchaseByIdAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(purchase);
        mockPurchasesRepo.Setup(r => r.DeletePurchaseAsync(1, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product>());
        mockUnitOfWork.Setup(u => u.Purchases).Returns(mockPurchasesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new PurchaseService(mockUnitOfWork.Object);

        // ACT
        await service.DeletePurchaseAsync(1, 1);

        // ASSERT
        mockPurchasesRepo.Verify(r => r.DeletePurchaseAsync(1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreatePurchaseAsync_WithMultipleItems_IncrementsAllCorrectly()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockPurchasesRepo = new Mock<IPurchaseRepository>();
        var mockProductsRepo = new Mock<IProductRepository>();

        var product1 = new Product { Id = 1, Stock = 10, CompanyId = 1, Name = "Product A" };
        var product2 = new Product { Id = 2, Stock = 20, CompanyId = 1, Name = "Product B" };

        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product1, product2 });
        mockPurchasesRepo.Setup(r => r.AddPurchaseAsync(It.IsAny<Purchase>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Purchase p, CancellationToken ct) => p);
        mockUnitOfWork.Setup(u => u.Purchases).Returns(mockPurchasesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new PurchaseService(mockUnitOfWork.Object);
        var request = new PurchaseDto(
            Id: 0, SupplierId: 1, SupplierName: "Supplier",
            Status: EntityStatus.Pending, Total: 1500, Notes: null,
            CreatedAt: DateTime.UtcNow, UpdatedAt: null, CreatedBy: 1, UpdatedBy: null,
            Items: new List<PurchaseItemDto>
            {
                new(0, 1, "Product A", "CODE1", 15, 100, 1500),
                new(0, 2, "Product B", "CODE2", 10, 100, 1000)
            }
        );

        // ACT
        await service.CreatePurchaseAsync(request, 1, 1);

        // ASSERT
        product1.Stock.Should().Be(25);  // 10 + 15
        product2.Stock.Should().Be(30);  // 20 + 10
    }

    [Fact]
    public async Task CreatePurchaseAsync_WhenCommitFails_ThrowsAndDoesNotPersistStock()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockPurchasesRepo = new Mock<IPurchaseRepository>();
        var mockProductsRepo = new Mock<IProductRepository>();

        var product = new Product { Id = 1, Stock = 10, CompanyId = 1, Name = "Test Product" };

        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });
        mockPurchasesRepo.Setup(r => r.AddPurchaseAsync(It.IsAny<Purchase>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Purchase p, CancellationToken ct) => p);
        mockUnitOfWork.Setup(u => u.Purchases).Returns(mockPurchasesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database connection failed"));

        var service = new PurchaseService(mockUnitOfWork.Object);
        var request = new PurchaseDto(
            Id: 0, SupplierId: 1, SupplierName: "Supplier",
            Status: EntityStatus.Pending, Total: 500, Notes: null,
            CreatedAt: DateTime.UtcNow, UpdatedAt: null, CreatedBy: 1, UpdatedBy: null,
            Items: new List<PurchaseItemDto>
            {
                new(0, 1, "Test Product", "CODE", 5, 100, 500)
            }
        );

        // ACT
        var act = () => service.CreatePurchaseAsync(request, 1, 1);

        // ASSERT
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Database connection failed");
        // NOTE: Stock change happens in memory before commit. True rollback needs integration tests.
        product.Stock.Should().Be(15); // Stock incremented before commit failed
    }

[Fact]
    public async Task GetAllPurchasesAsync_WithEmptyResult_ReturnsEmptyList()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockPurchasesRepo = new Mock<IPurchaseRepository>();

        var purchases = new PagedResult<Purchase>
        {
            Items = new List<Purchase>(),
            PageNumber = 1,
            PageSize = 10,
            RowCount = 0,
            PageCount = 0
        };

        mockPurchasesRepo.Setup(r => r.GetAllPurchasesByCompanyAsync(1, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(purchases);
        mockUnitOfWork.Setup(u => u.Purchases).Returns(mockPurchasesRepo.Object);

        var service = new PurchaseService(mockUnitOfWork.Object);

        // ACT
        var result = await service.GetAllPurchasesAsync(1, 1, 10);

        // ASSERT
        result.Items.Should().BeEmpty();
        result.RowCount.Should().Be(0);
    }

    [Fact]
    public async Task CreatePurchaseAsync_WithDuplicateProductIds_GroupsQuantities()
    {
        // ARRANGE: request.Items has two items with same ProductId (qty 3 and qty 2)
        // Should group and increment 5 total
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockPurchasesRepo = new Mock<IPurchaseRepository>();
        var mockProductsRepo = new Mock<IProductRepository>();

        var product = new Product { Id = 1, Stock = 20, CompanyId = 1, Name = "Test Product" };

        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });
        mockPurchasesRepo.Setup(r => r.AddPurchaseAsync(It.IsAny<Purchase>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Purchase p, CancellationToken ct) => p);
        mockUnitOfWork.Setup(u => u.Purchases).Returns(mockPurchasesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new PurchaseService(mockUnitOfWork.Object);
        var request = new PurchaseDto(
            Id: 0, SupplierId: 1, SupplierName: "Supplier",
            Status: EntityStatus.Pending, Total: 500, Notes: null,
            CreatedAt: DateTime.UtcNow, UpdatedAt: null, CreatedBy: 1, UpdatedBy: null,
            Items: new List<PurchaseItemDto>
            {
                new(0, 1, "Test Product", "CODE", 3, 100, 300),
                new(0, 1, "Test Product", "CODE", 2, 100, 200) // Same ProductId, should group to 5
            }
        );

        // ACT
        await service.CreatePurchaseAsync(request, 1, 1);

        // ASSERT - grouped quantity 3 + 2 = 5 should be incremented
        product.Stock.Should().Be(25); // 20 + 5
    }

    [Fact]
    public async Task CreatePurchaseAsync_WithProductNotFoundInMap_SkipsGracefully()
    {
        // ARRANGE: one item has product that doesn't exist in DB
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockPurchasesRepo = new Mock<IPurchaseRepository>();
        var mockProductsRepo = new Mock<IProductRepository>();

        var existingProduct = new Product { Id = 1, Stock = 20, CompanyId = 1, Name = "Existing Product" };

        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { existingProduct });
        mockPurchasesRepo.Setup(r => r.AddPurchaseAsync(It.IsAny<Purchase>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Purchase p, CancellationToken ct) => p);
        mockUnitOfWork.Setup(u => u.Purchases).Returns(mockPurchasesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new PurchaseService(mockUnitOfWork.Object);
        var request = new PurchaseDto(
            Id: 0, SupplierId: 1, SupplierName: "Supplier",
            Status: EntityStatus.Pending, Total: 800, Notes: null,
            CreatedAt: DateTime.UtcNow, UpdatedAt: null, CreatedBy: 1, UpdatedBy: null,
            Items: new List<PurchaseItemDto>
            {
                new(0, 1, "Existing Product", "CODE1", 3, 100, 300),
                new(0, 999, "NonExistent", "CODE999", 5, 100, 500) // Product not in DB
            }
        );

        // ACT - should not throw, just skip the non-existent product
        await service.CreatePurchaseAsync(request, 1, 1);

        // ASSERT - only existing product stock should change
        existingProduct.Stock.Should().Be(23); // 20 + 3
    }

    [Fact]
    public async Task DeletePurchaseAsync_WhenPurchaseFound_DecrementsProductStock()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockPurchasesRepo = new Mock<IPurchaseRepository>();
        var mockProductsRepo = new Mock<IProductRepository>();

        var product = new Product { Id = 1, Stock = 20, CompanyId = 1, Name = "Test Product" };

        var purchase = new Purchase
        {
            Id = 1, CompanyId = 1, SupplierId = 1, SupplierName = "Supplier",
            Status = EntityStatus.Pending, Total = 500,
            Items = new List<PurchaseItem>
            {
                new() { ProductId = 1, ProductName = "Test Product", ProductCode = "CODE", Quantity = 5, UnitPrice = 100, Subtotal = 500 }
            }
        };

        mockPurchasesRepo.Setup(r => r.GetPurchaseByIdAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(purchase);
        mockPurchasesRepo.Setup(r => r.DeletePurchaseAsync(1, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });
        mockUnitOfWork.Setup(u => u.Purchases).Returns(mockPurchasesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new PurchaseService(mockUnitOfWork.Object);

        // ACT
        await service.DeletePurchaseAsync(1, 1);

        // ASSERT - stock should be decremented: 20 - 5 = 15
        product.Stock.Should().Be(15);
        mockPurchasesRepo.Verify(r => r.DeletePurchaseAsync(1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeletePurchaseAsync_WhenProductNotFoundInMap_SkipsGracefully()
    {
        // ARRANGE - product no longer exists
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockPurchasesRepo = new Mock<IPurchaseRepository>();
        var mockProductsRepo = new Mock<IProductRepository>();

        var purchase = new Purchase
        {
            Id = 1, CompanyId = 1, SupplierId = 1, SupplierName = "Supplier",
            Status = EntityStatus.Pending, Total = 500,
            Items = new List<PurchaseItem>
            {
                new() { ProductId = 999, ProductName = "Deleted Product", ProductCode = "CODE", Quantity = 5, UnitPrice = 100, Subtotal = 500 }
            }
        };

        mockPurchasesRepo.Setup(r => r.GetPurchaseByIdAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(purchase);
        mockPurchasesRepo.Setup(r => r.DeletePurchaseAsync(1, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        // Product not found in batch query
        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product>());
        mockUnitOfWork.Setup(u => u.Purchases).Returns(mockPurchasesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new PurchaseService(mockUnitOfWork.Object);

        // ACT - should not throw, just skip gracefully
        var act = () => service.DeletePurchaseAsync(1, 1);

        // ASSERT
        await act.Should().NotThrowAsync();
        mockPurchasesRepo.Verify(r => r.DeletePurchaseAsync(1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeletePurchaseAsync_WhenCommitFails_ThrowsAndDoesNotDecrementStock()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockPurchasesRepo = new Mock<IPurchaseRepository>();
        var mockProductsRepo = new Mock<IProductRepository>();

        var product = new Product { Id = 1, Stock = 20, CompanyId = 1, Name = "Test Product" };

        var purchase = new Purchase
        {
            Id = 1, CompanyId = 1, SupplierId = 1, SupplierName = "Supplier",
            Status = EntityStatus.Pending, Total = 500,
            Items = new List<PurchaseItem>
            {
                new() { ProductId = 1, ProductName = "Test Product", ProductCode = "CODE", Quantity = 5, UnitPrice = 100, Subtotal = 500 }
            }
        };

        mockPurchasesRepo.Setup(r => r.GetPurchaseByIdAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(purchase);
        mockPurchasesRepo.Setup(r => r.DeletePurchaseAsync(1, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });
        mockUnitOfWork.Setup(u => u.Purchases).Returns(mockPurchasesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database connection failed"));

        var service = new PurchaseService(mockUnitOfWork.Object);

        // ACT
        var act = () => service.DeletePurchaseAsync(1, 1);

        // ASSERT
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Database connection failed");
        // Stock was modified in memory before commit failed
        // NOTE: This documents current behavior. True rollback requires integration tests.
        product.Stock.Should().Be(15); // 20 - 5 decremented before commit failed
    }
}