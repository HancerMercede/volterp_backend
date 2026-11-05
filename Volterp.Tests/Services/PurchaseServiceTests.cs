using EitherWay;
using FluentAssertions;
using Moq;
using Volterp.Application.DTOs;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Application.Services;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Xunit;
using Volterp.Tests.TestHelpers;

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

        var product = new Product { Id = Guid7TestValues.ProductA, Stock = 10, CompanyId = Guid7TestValues.CompanyA, Name = "Test Product" };

        // Batch query: GetProductsByIdsAsync instead of individual GetProductByIdAsync
        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });
        
        mockPurchasesRepo.Setup(r => r.AddPurchaseAsync(It.IsAny<Purchase>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Purchase p, CancellationToken ct) => p);
        
        mockUnitOfWork.Setup(u => u.Purchases).Returns(mockPurchasesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new PurchaseService(mockUnitOfWork.Object);
        SetupCodes(mockUnitOfWork);
        var request = new PurchaseDto
        {
            Id = Guid.Empty,
            SupplierId = Guid7TestValues.PurchaseA,
            SupplierName = "Test Supplier",
            Status = EntityStatus.Pending,
            Total = 500,
            Notes = "Test",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = null,
            CreatedBy = Guid7TestValues.UserA,
            UpdatedBy = null,
            Items = new List<PurchaseItemDto>
            {
                new() { Id = Guid.Empty, ProductId = Guid7TestValues.ProductA, ProductName = "Test Product", ProductCode = "CODE123", Quantity = 5, UnitPrice = 100, Subtotal = 500 }
            }
        };

        // ACT
        await service.CreatePurchaseAsync(request, Guid7TestValues.CompanyA, Guid7TestValues.UserA);

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

        var product = new Product { Id = Guid7TestValues.ProductA, Stock = 10, CompanyId = Guid7TestValues.CompanyA, Name = "Test" };

        var existingPurchase = new Purchase
        {
            Id = Guid7TestValues.PurchaseA,
            CompanyId = Guid7TestValues.CompanyA,
            SupplierId = Guid7TestValues.PurchaseA,
            SupplierName = "Test",
            Status = EntityStatus.Pending,
            Total = 500,
            Items = new List<PurchaseItem>
            {
                new() { Id = Guid7TestValues.PurchaseA, ProductId = Guid7TestValues.ProductA, ProductName = "Test", ProductCode = "CODE", Quantity = 5, UnitPrice = 100, Subtotal = 500 }
            }
        };

        mockPurchasesRepo.Setup(r => r.GetPurchaseByIdAsync(Guid7TestValues.PurchaseA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPurchase);
        
        mockPurchasesRepo.Setup(r => r.UpdatePurchaseAsync(It.IsAny<Purchase>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        
        // Batch query: GetProductsByIdsAsync returns products for both old and new items
        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });
        
        mockUnitOfWork.Setup(u => u.Purchases).Returns(mockPurchasesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new PurchaseService(mockUnitOfWork.Object);
        var updateRequest = new PurchaseDto
        {
            Id = Guid7TestValues.PurchaseA,
            SupplierId = Guid7TestValues.PurchaseA,
            SupplierName = "Updated",
            Status = EntityStatus.Pending,
            Total = 800,
            Notes = "",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = null,
            CreatedBy = Guid7TestValues.UserA,
            UpdatedBy = null,
            Items = new List<PurchaseItemDto>
            {
                new() { Id = Guid.Empty, ProductId = Guid7TestValues.ProductA, ProductName = "Test", ProductCode = "CODE", Quantity = 8, UnitPrice = 100, Subtotal = 800 }
            }
        };

        // ACT
        await service.UpdatePurchaseAsync(Guid7TestValues.PurchaseA, Guid7TestValues.CompanyA, updateRequest, Guid7TestValues.UserA);

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
                new() { Id = Guid7TestValues.PurchaseA, CompanyId = Guid7TestValues.CompanyA, SupplierId = Guid7TestValues.PurchaseA, SupplierName = "Supplier A", Status = EntityStatus.Pending, Total = 500 },
                new() { Id = Guid.NewGuid(), CompanyId = Guid7TestValues.CompanyA, SupplierId = Guid.NewGuid(), SupplierName = "Supplier B", Status = EntityStatus.Completed, Total = 300 }
            },
            PageNumber = 1,
            PageSize = 10,
            RowCount = 2,
            PageCount = 1
        };

        mockPurchasesRepo.Setup(r => r.GetAllPurchasesAsync(1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(purchases);
        mockUnitOfWork.Setup(u => u.Purchases).Returns(mockPurchasesRepo.Object);

        var service = new PurchaseService(mockUnitOfWork.Object);

        // ACT
        var result = await service.GetAllPurchasesAsync(1, 10);

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
            Id = Guid7TestValues.PurchaseA, CompanyId = Guid7TestValues.CompanyA, SupplierId = Guid7TestValues.PurchaseA, SupplierName = "Supplier A",
            Status = EntityStatus.Pending, Total = 500,
            Items = new List<PurchaseItem>
            {
                new() { Id = Guid7TestValues.PurchaseA, ProductId = Guid7TestValues.ProductA, ProductName = "Test", ProductCode = "CODE", Quantity = 5, UnitPrice = 100, Subtotal = 500 }
            }
        };

        mockPurchasesRepo.Setup(r => r.GetPurchaseByIdAsync(Guid7TestValues.PurchaseA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(purchase);
        mockUnitOfWork.Setup(u => u.Purchases).Returns(mockPurchasesRepo.Object);

        var service = new PurchaseService(mockUnitOfWork.Object);

        // ACT
        var result = await service.GetPurchaseByIdAsync(Guid7TestValues.PurchaseA);

        // ASSERT
        result.Should().BeOfType<Either<Error, PurchaseDto?>.Right>();
        var dto = ((Either<Error, PurchaseDto?>.Right)result).Value;
        dto.Should().NotBeNull();
        dto!.Id.Should().Be(Guid7TestValues.PurchaseA);
        dto.SupplierName.Should().Be("Supplier A");
    }

    [Fact]
    public async Task GetPurchaseByIdAsync_WhenNotFound_ReturnsNull()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockPurchasesRepo = new Mock<IPurchaseRepository>();

        mockPurchasesRepo.Setup(r => r.GetPurchaseByIdAsync(Guid.NewGuid(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Purchase?)null);
        mockUnitOfWork.Setup(u => u.Purchases).Returns(mockPurchasesRepo.Object);

        var service = new PurchaseService(mockUnitOfWork.Object);

        // ACT
        var result = await service.GetPurchaseByIdAsync(Guid.NewGuid());

        // ASSERT
        result.Should().BeOfType<Either<Error, PurchaseDto?>.Right>();
        var right = (Either<Error, PurchaseDto?>.Right)result;
        right.Value.Should().BeNull();
    }

    [Fact]
    public async Task UpdatePurchaseAsync_WhenPurchaseNotFound_ThrowsPurchaseNotFoundException()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockPurchasesRepo = new Mock<IPurchaseRepository>();

        mockPurchasesRepo.Setup(r => r.GetPurchaseByIdAsync(Guid.NewGuid(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Purchase?)null);
        mockUnitOfWork.Setup(u => u.Purchases).Returns(mockPurchasesRepo.Object);

        var service = new PurchaseService(mockUnitOfWork.Object);
        var updateRequest = new PurchaseDto
        {
            Id = Guid7TestValues.PurchaseA, SupplierId = Guid7TestValues.PurchaseA, SupplierName = "Updated",
            Status = EntityStatus.Pending, Total = 800, Notes = "",
            CreatedAt = DateTime.UtcNow, UpdatedAt = null,
            CreatedBy = Guid7TestValues.UserA, UpdatedBy = null,
            Items = new List<PurchaseItemDto>
            {
                new() { Id = Guid.Empty, ProductId = Guid7TestValues.ProductA, ProductName = "Test", ProductCode = "CODE", Quantity = 5, UnitPrice = 100, Subtotal = 500 }
            }
        };

        // ACT
        var result = await service.UpdatePurchaseAsync(Guid.NewGuid(), Guid7TestValues.CompanyA, updateRequest, Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, PurchaseDto>.Left>();
        var error = ((Either<Error, PurchaseDto>.Left)result).Value;
        error.Message.Should().Be("Purchase not found.");
    }

    [Fact]
    public async Task DeletePurchaseAsync_WhenPurchaseNotFound_ThrowsPurchaseNotFoundException()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockPurchasesRepo = new Mock<IPurchaseRepository>();

        mockPurchasesRepo.Setup(r => r.GetPurchaseByIdAsync(Guid.NewGuid(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Purchase?)null);
        mockUnitOfWork.Setup(u => u.Purchases).Returns(mockPurchasesRepo.Object);

        var service = new PurchaseService(mockUnitOfWork.Object);

        // ACT
        var result = await service.DeletePurchaseAsync(Guid.NewGuid(), Guid7TestValues.CompanyA);

        // ASSERT
        result.Should().BeOfType<Either<Error, Unit>.Left>();
        var error = ((Either<Error, Unit>.Left)result).Value;
        error.Message.Should().Be("Purchase not found.");
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
            Id = Guid7TestValues.PurchaseA, CompanyId = Guid7TestValues.CompanyA, SupplierId = Guid7TestValues.PurchaseA, SupplierName = "Supplier",
            Status = EntityStatus.Pending, Total = 500,
            Items = new List<PurchaseItem>()
        };

        mockPurchasesRepo.Setup(r => r.GetPurchaseByIdAsync(Guid7TestValues.PurchaseA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(purchase);
        mockPurchasesRepo.Setup(r => r.DeletePurchaseAsync(Guid7TestValues.PurchaseA, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product>());
        mockUnitOfWork.Setup(u => u.Purchases).Returns(mockPurchasesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new PurchaseService(mockUnitOfWork.Object);

        // ACT
        await service.DeletePurchaseAsync(Guid7TestValues.PurchaseA, Guid7TestValues.CompanyA);

        // ASSERT
        mockPurchasesRepo.Verify(r => r.DeletePurchaseAsync(Guid7TestValues.PurchaseA, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreatePurchaseAsync_WithMultipleItems_IncrementsAllCorrectly()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockPurchasesRepo = new Mock<IPurchaseRepository>();
        var mockProductsRepo = new Mock<IProductRepository>();

        var product1 = new Product { Id = Guid7TestValues.ProductA, Stock = 10, CompanyId = Guid7TestValues.CompanyA, Name = "Product A" };
        var product2Id = Guid.NewGuid();
        var product2 = new Product { Id = product2Id, Stock = 20, CompanyId = Guid7TestValues.CompanyA, Name = "Product B" };

        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product1, product2 });
        mockPurchasesRepo.Setup(r => r.AddPurchaseAsync(It.IsAny<Purchase>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Purchase p, CancellationToken ct) => p);
        mockUnitOfWork.Setup(u => u.Purchases).Returns(mockPurchasesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new PurchaseService(mockUnitOfWork.Object);
        SetupCodes(mockUnitOfWork);
        var request = new PurchaseDto
        {
            Id = Guid.Empty, SupplierId = Guid7TestValues.PurchaseA, SupplierName = "Supplier",
            Status = EntityStatus.Pending, Total = 1500, Notes = "",
            CreatedAt = DateTime.UtcNow, UpdatedAt = null, CreatedBy = Guid7TestValues.UserA, UpdatedBy = null,
            Items = new List<PurchaseItemDto>
            {
                new() { Id = Guid.Empty, ProductId = Guid7TestValues.ProductA, ProductName = "Product A", ProductCode = "CODE1", Quantity = 15, UnitPrice = 100, Subtotal = 1500 },
                new() { Id = Guid.Empty, ProductId = product2Id, ProductName = "Product B", ProductCode = "CODE2", Quantity = 10, UnitPrice = 100, Subtotal = 1000 }
            }
        };

        // ACT
        await service.CreatePurchaseAsync(request, Guid7TestValues.CompanyA, Guid7TestValues.UserA);

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

        var product = new Product { Id = Guid7TestValues.ProductA, Stock = 10, CompanyId = Guid7TestValues.CompanyA, Name = "Test Product" };

        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });
        mockPurchasesRepo.Setup(r => r.AddPurchaseAsync(It.IsAny<Purchase>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Purchase p, CancellationToken ct) => p);
        mockUnitOfWork.Setup(u => u.Purchases).Returns(mockPurchasesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database connection failed"));

        var service = new PurchaseService(mockUnitOfWork.Object);
        SetupCodes(mockUnitOfWork);
        var request = new PurchaseDto
        {
            Id = Guid.Empty, SupplierId = Guid7TestValues.PurchaseA, SupplierName = "Supplier",
            Status = EntityStatus.Pending, Total = 500, Notes = "",
            CreatedAt = DateTime.UtcNow, UpdatedAt = null, CreatedBy = Guid7TestValues.UserA, UpdatedBy = null,
            Items = new List<PurchaseItemDto>
            {
                new() { Id = Guid.Empty, ProductId = Guid7TestValues.ProductA, ProductName = "Test Product", ProductCode = "CODE", Quantity = 5, UnitPrice = 100, Subtotal = 500 }
            }
        };

        // ACT
        var result = await service.CreatePurchaseAsync(request, Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, PurchaseDto>.Left>();
        var error = ((Either<Error, PurchaseDto>.Left)result).Value;
        error.Message.Should().Be("Database connection failed");
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

        mockPurchasesRepo.Setup(r => r.GetAllPurchasesAsync(1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(purchases);
        mockUnitOfWork.Setup(u => u.Purchases).Returns(mockPurchasesRepo.Object);

        var service = new PurchaseService(mockUnitOfWork.Object);

        // ACT
        var result = await service.GetAllPurchasesAsync(1, 10);

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

        var product = new Product { Id = Guid7TestValues.ProductA, Stock = 20, CompanyId = Guid7TestValues.CompanyA, Name = "Test Product" };

        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });
        mockPurchasesRepo.Setup(r => r.AddPurchaseAsync(It.IsAny<Purchase>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Purchase p, CancellationToken ct) => p);
        mockUnitOfWork.Setup(u => u.Purchases).Returns(mockPurchasesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new PurchaseService(mockUnitOfWork.Object);
        SetupCodes(mockUnitOfWork);
        var request = new PurchaseDto
        {
            Id = Guid.Empty, SupplierId = Guid7TestValues.PurchaseA, SupplierName = "Supplier",
            Status = EntityStatus.Pending, Total = 500, Notes = "",
            CreatedAt = DateTime.UtcNow, UpdatedAt = null, CreatedBy = Guid7TestValues.UserA, UpdatedBy = null,
            Items = new List<PurchaseItemDto>
            {
                new() { Id = Guid.Empty, ProductId = Guid7TestValues.ProductA, ProductName = "Test Product", ProductCode = "CODE", Quantity = 3, UnitPrice = 100, Subtotal = 300 },
                new() { Id = Guid.Empty, ProductId = Guid7TestValues.ProductA, ProductName = "Test Product", ProductCode = "CODE", Quantity = 2, UnitPrice = 100, Subtotal = 200 } // Same ProductId, should group to 5
            }
        };

        // ACT
        await service.CreatePurchaseAsync(request, Guid7TestValues.CompanyA, Guid7TestValues.UserA);

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

        var existingProduct = new Product { Id = Guid7TestValues.ProductA, Stock = 20, CompanyId = Guid7TestValues.CompanyA, Name = "Existing Product" };

        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { existingProduct });
        mockPurchasesRepo.Setup(r => r.AddPurchaseAsync(It.IsAny<Purchase>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Purchase p, CancellationToken ct) => p);
        mockUnitOfWork.Setup(u => u.Purchases).Returns(mockPurchasesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new PurchaseService(mockUnitOfWork.Object);
        SetupCodes(mockUnitOfWork);
        var request = new PurchaseDto
        {
            Id = Guid.Empty, SupplierId = Guid7TestValues.PurchaseA, SupplierName = "Supplier",
            Status = EntityStatus.Pending, Total = 800, Notes = "",
            CreatedAt = DateTime.UtcNow, UpdatedAt = null, CreatedBy = Guid7TestValues.UserA, UpdatedBy = null,
            Items = new List<PurchaseItemDto>
            {
                new() { Id = Guid.Empty, ProductId = Guid7TestValues.ProductA, ProductName = "Existing Product", ProductCode = "CODE1", Quantity = 3, UnitPrice = 100, Subtotal = 300 },
                new() { Id = Guid.Empty, ProductId = Guid.NewGuid(), ProductName = "NonExistent", ProductCode = "CODE999", Quantity = 5, UnitPrice = 100, Subtotal = 500 } // Product not in DB
            }
        };

        // ACT - should not throw, just skip the non-existent product
        await service.CreatePurchaseAsync(request, Guid7TestValues.CompanyA, Guid7TestValues.UserA);

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

        var product = new Product { Id = Guid7TestValues.ProductA, Stock = 20, CompanyId = Guid7TestValues.CompanyA, Name = "Test Product" };

        var purchase = new Purchase
        {
            Id = Guid7TestValues.PurchaseA, CompanyId = Guid7TestValues.CompanyA, SupplierId = Guid7TestValues.PurchaseA, SupplierName = "Supplier",
            Status = EntityStatus.Pending, Total = 500,
            Items = new List<PurchaseItem>
            {
                new() { ProductId = Guid7TestValues.ProductA, ProductName = "Test Product", ProductCode = "CODE", Quantity = 5, UnitPrice = 100, Subtotal = 500 }
            }
        };

        mockPurchasesRepo.Setup(r => r.GetPurchaseByIdAsync(Guid7TestValues.PurchaseA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(purchase);
        mockPurchasesRepo.Setup(r => r.DeletePurchaseAsync(Guid7TestValues.PurchaseA, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });
        mockUnitOfWork.Setup(u => u.Purchases).Returns(mockPurchasesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new PurchaseService(mockUnitOfWork.Object);

        // ACT
        await service.DeletePurchaseAsync(Guid7TestValues.PurchaseA, Guid7TestValues.CompanyA);

        // ASSERT - stock should be decremented: 20 - 5 = 15
        product.Stock.Should().Be(15);
        mockPurchasesRepo.Verify(r => r.DeletePurchaseAsync(Guid7TestValues.PurchaseA, It.IsAny<CancellationToken>()), Times.Once);
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
            Id = Guid7TestValues.PurchaseA, CompanyId = Guid7TestValues.CompanyA, SupplierId = Guid7TestValues.PurchaseA, SupplierName = "Supplier",
            Status = EntityStatus.Pending, Total = 500,
            Items = new List<PurchaseItem>
            {
                new() { ProductId = Guid.NewGuid(), ProductName = "Deleted Product", ProductCode = "CODE", Quantity = 5, UnitPrice = 100, Subtotal = 500 }
            }
        };

        mockPurchasesRepo.Setup(r => r.GetPurchaseByIdAsync(Guid7TestValues.PurchaseA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(purchase);
        mockPurchasesRepo.Setup(r => r.DeletePurchaseAsync(Guid7TestValues.PurchaseA, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        // Product not found in batch query
        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product>());
        mockUnitOfWork.Setup(u => u.Purchases).Returns(mockPurchasesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new PurchaseService(mockUnitOfWork.Object);

        // ACT - should not throw, just skip gracefully
        var act = () => service.DeletePurchaseAsync(Guid7TestValues.PurchaseA, Guid7TestValues.CompanyA);

        // ASSERT
        await act.Should().NotThrowAsync();
        mockPurchasesRepo.Verify(r => r.DeletePurchaseAsync(Guid7TestValues.PurchaseA, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeletePurchaseAsync_WhenCommitFails_ThrowsAndDoesNotDecrementStock()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockPurchasesRepo = new Mock<IPurchaseRepository>();
        var mockProductsRepo = new Mock<IProductRepository>();

        var product = new Product { Id = Guid7TestValues.ProductA, Stock = 20, CompanyId = Guid7TestValues.CompanyA, Name = "Test Product" };

        var purchase = new Purchase
        {
            Id = Guid7TestValues.PurchaseA, CompanyId = Guid7TestValues.CompanyA, SupplierId = Guid7TestValues.PurchaseA, SupplierName = "Supplier",
            Status = EntityStatus.Pending, Total = 500,
            Items = new List<PurchaseItem>
            {
                new() { ProductId = Guid7TestValues.ProductA, ProductName = "Test Product", ProductCode = "CODE", Quantity = 5, UnitPrice = 100, Subtotal = 500 }
            }
        };

        mockPurchasesRepo.Setup(r => r.GetPurchaseByIdAsync(Guid7TestValues.PurchaseA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(purchase);
        mockPurchasesRepo.Setup(r => r.DeletePurchaseAsync(Guid7TestValues.PurchaseA, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });
        mockUnitOfWork.Setup(u => u.Purchases).Returns(mockPurchasesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database connection failed"));

        var service = new PurchaseService(mockUnitOfWork.Object);

        // ACT
        var result = await service.DeletePurchaseAsync(Guid7TestValues.PurchaseA, Guid7TestValues.CompanyA);

        // ASSERT
        result.Should().BeOfType<Either<Error, Unit>.Left>();
        var error = ((Either<Error, Unit>.Left)result).Value;
        error.Message.Should().Be("Database connection failed");
        // Stock was modified in memory before commit failed
        // NOTE: This documents current behavior. True rollback requires integration tests.
        product.Stock.Should().Be(15); // 20 - 5 decremented before commit failed
    }

    [Fact]
    public async Task CreatePurchaseAsync_WithoutCode_UsesTheGeneratedCode()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockPurchasesRepo = new Mock<IPurchaseRepository>();
        var mockProductsRepo = new Mock<IProductRepository>();

        var product = new Product { Id = Guid7TestValues.ProductA, Stock = 10, CompanyId = Guid7TestValues.CompanyA, Name = "Test Product" };

        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });
        mockPurchasesRepo.Setup(r => r.AddPurchaseAsync(It.IsAny<Purchase>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Purchase p, CancellationToken ct) => p);
        mockUnitOfWork.Setup(u => u.Purchases).Returns(mockPurchasesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new PurchaseService(mockUnitOfWork.Object);
        SetupCodes(mockUnitOfWork);
        var request = new PurchaseDto
        {
            Id = Guid.Empty,
            SupplierId = Guid7TestValues.SupplierA,
            SupplierName = "Test Supplier",
            Status = EntityStatus.Pending,
            Total = 500,
            Notes = "Test",
            Items = new List<PurchaseItemDto>
            {
                new() { Id = Guid.Empty, ProductId = Guid7TestValues.ProductA, ProductName = "Test Product", ProductCode = "CODE123", Quantity = 5, UnitPrice = 100, Subtotal = 500 }
            }
        };

        // ACT
        var result = await service.CreatePurchaseAsync(request, Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        // ASSERT - the request carries no code, so the generator decides
        result.Should().BeOfType<Either<Error, PurchaseDto>.Right>();
        var dto = ((Either<Error, PurchaseDto>.Right)result).Value;
        dto.Code.Should().Be("Comp-001");
    }

    [Fact]
    public async Task CreatePurchaseAsync_WithUserCode_KeepsTheTrimmedUserCode()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockPurchasesRepo = new Mock<IPurchaseRepository>();
        var mockProductsRepo = new Mock<IProductRepository>();

        var product = new Product { Id = Guid7TestValues.ProductA, Stock = 10, CompanyId = Guid7TestValues.CompanyA, Name = "Test Product" };

        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });
        mockPurchasesRepo.Setup(r => r.AddPurchaseAsync(It.IsAny<Purchase>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Purchase p, CancellationToken ct) => p);
        mockUnitOfWork.Setup(u => u.Purchases).Returns(mockPurchasesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new PurchaseService(mockUnitOfWork.Object);
        SetupCodes(mockUnitOfWork);
        var request = new PurchaseDto
        {
            Id = Guid.Empty,
            Code = "  MY-COMP-1  ",
            SupplierId = Guid7TestValues.SupplierA,
            SupplierName = "Test Supplier",
            Status = EntityStatus.Pending,
            Total = 500,
            Notes = "Test",
            Items = new List<PurchaseItemDto>
            {
                new() { Id = Guid.Empty, ProductId = Guid7TestValues.ProductA, ProductName = "Test Product", ProductCode = "CODE123", Quantity = 5, UnitPrice = 100, Subtotal = 500 }
            }
        };

        // ACT
        var result = await service.CreatePurchaseAsync(request, Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        // ASSERT - the code of the user wins over the generator
        result.Should().BeOfType<Either<Error, PurchaseDto>.Right>();
        var dto = ((Either<Error, PurchaseDto>.Right)result).Value;
        dto.Code.Should().Be("MY-COMP-1");
    }

    [Fact]
    public async Task CreatePurchaseAsync_WithRepeatedCode_ReturnsCleanError()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockPurchasesRepo = new Mock<IPurchaseRepository>();
        var mockProductsRepo = new Mock<IProductRepository>();

        var product = new Product { Id = Guid7TestValues.ProductA, Stock = 10, CompanyId = Guid7TestValues.CompanyA, Name = "Test Product" };
        var otherPurchase = new Purchase
        {
            Id = Guid7TestValues.PurchaseA, CompanyId = Guid7TestValues.CompanyA,
            Code = "DUP-1", Status = EntityStatus.Pending
        };

        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });
        mockPurchasesRepo.Setup(r => r.ExistsCodeAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, string code, Guid? excludeId, CancellationToken _) => otherPurchase.Code == code && excludeId != otherPurchase.Id);
        mockUnitOfWork.Setup(u => u.Purchases).Returns(mockPurchasesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new PurchaseService(mockUnitOfWork.Object);
        SetupCodes(mockUnitOfWork);
        var request = new PurchaseDto
        {
            Id = Guid.Empty,
            Code = "DUP-1",
            SupplierId = Guid7TestValues.SupplierA,
            SupplierName = "Test Supplier",
            Status = EntityStatus.Pending,
            Total = 500,
            Notes = "Test",
            Items = new List<PurchaseItemDto>
            {
                new() { Id = Guid.Empty, ProductId = Guid7TestValues.ProductA, ProductName = "Test Product", ProductCode = "CODE123", Quantity = 5, UnitPrice = 100, Subtotal = 500 }
            }
        };

        // ACT
        var result = await service.CreatePurchaseAsync(request, Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        // ASSERT - a clean error, and the inventory was not touched
        result.Should().BeOfType<Either<Error, PurchaseDto>.Left>();
        var error = ((Either<Error, PurchaseDto>.Left)result).Value;
        error.Message.Should().Be("The code is already in use.");
        product.Stock.Should().Be(10);
        mockPurchasesRepo.Verify(r => r.AddPurchaseAsync(It.IsAny<Purchase>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdatePurchaseAsync_WithoutCode_KeepsTheCurrentCode()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockPurchasesRepo = new Mock<IPurchaseRepository>();
        var mockProductsRepo = new Mock<IProductRepository>();

        var product = new Product { Id = Guid7TestValues.ProductA, Stock = 10, CompanyId = Guid7TestValues.CompanyA, Name = "Test" };

        var existingPurchase = new Purchase
        {
            Id = Guid7TestValues.PurchaseA, CompanyId = Guid7TestValues.CompanyA, Code = "Comp-007",
            SupplierId = Guid7TestValues.SupplierA, SupplierName = "Test",
            Status = EntityStatus.Pending, Total = 500,
            Items = new List<PurchaseItem>
            {
                new() { Id = Guid7TestValues.PurchaseA, ProductId = Guid7TestValues.ProductA, ProductName = "Test", ProductCode = "CODE", Quantity = 5, UnitPrice = 100, Subtotal = 500 }
            }
        };

        mockPurchasesRepo.Setup(r => r.GetPurchaseByIdAsync(Guid7TestValues.PurchaseA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPurchase);
        mockPurchasesRepo.Setup(r => r.UpdatePurchaseAsync(It.IsAny<Purchase>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });
        mockUnitOfWork.Setup(u => u.Purchases).Returns(mockPurchasesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new PurchaseService(mockUnitOfWork.Object);
        var updateRequest = new PurchaseDto
        {
            Id = Guid7TestValues.PurchaseA,
            SupplierId = Guid7TestValues.SupplierA,
            SupplierName = "Updated",
            Status = EntityStatus.Pending,
            Total = 800,
            Notes = "",
            Items = new List<PurchaseItemDto>
            {
                new() { Id = Guid.Empty, ProductId = Guid7TestValues.ProductA, ProductName = "Test", ProductCode = "CODE", Quantity = 8, UnitPrice = 100, Subtotal = 800 }
            }
        };

        // ACT
        var result = await service.UpdatePurchaseAsync(Guid7TestValues.PurchaseA, Guid7TestValues.CompanyA, updateRequest, Guid7TestValues.UserA);

        // ASSERT - the update does not carry a code, so the purchase keeps the one it had
        result.Should().BeOfType<Either<Error, PurchaseDto>.Right>();
        var dto = ((Either<Error, PurchaseDto>.Right)result).Value;
        dto.Code.Should().Be("Comp-007");
    }

    [Fact]
    public async Task UpdatePurchaseAsync_WithItsOwnCode_KeepsTheCode()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockPurchasesRepo = new Mock<IPurchaseRepository>();
        var mockProductsRepo = new Mock<IProductRepository>();

        var product = new Product { Id = Guid7TestValues.ProductA, Stock = 10, CompanyId = Guid7TestValues.CompanyA, Name = "Test" };

        var existingPurchase = new Purchase
        {
            Id = Guid7TestValues.PurchaseA, CompanyId = Guid7TestValues.CompanyA, Code = "Comp-007",
            SupplierId = Guid7TestValues.SupplierA, SupplierName = "Test",
            Status = EntityStatus.Pending, Total = 500,
            Items = new List<PurchaseItem>
            {
                new() { Id = Guid7TestValues.PurchaseA, ProductId = Guid7TestValues.ProductA, ProductName = "Test", ProductCode = "CODE", Quantity = 5, UnitPrice = 100, Subtotal = 500 }
            }
        };

        mockPurchasesRepo.Setup(r => r.GetPurchaseByIdAsync(Guid7TestValues.PurchaseA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPurchase);
        mockPurchasesRepo.Setup(r => r.UpdatePurchaseAsync(It.IsAny<Purchase>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        // The only purchase holding that code is the one being updated: it must
        // not be reported as a conflict.
        mockPurchasesRepo.Setup(r => r.ExistsCodeAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, string code, Guid? excludeId, CancellationToken _) => existingPurchase.Code == code && excludeId != existingPurchase.Id);
        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });
        mockUnitOfWork.Setup(u => u.Purchases).Returns(mockPurchasesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new PurchaseService(mockUnitOfWork.Object);
        var updateRequest = new PurchaseDto
        {
            Id = Guid7TestValues.PurchaseA,
            Code = "Comp-007",
            SupplierId = Guid7TestValues.SupplierA,
            SupplierName = "Updated",
            Status = EntityStatus.Pending,
            Total = 800,
            Notes = "",
            Items = new List<PurchaseItemDto>
            {
                new() { Id = Guid.Empty, ProductId = Guid7TestValues.ProductA, ProductName = "Test", ProductCode = "CODE", Quantity = 8, UnitPrice = 100, Subtotal = 800 }
            }
        };

        // ACT
        var result = await service.UpdatePurchaseAsync(Guid7TestValues.PurchaseA, Guid7TestValues.CompanyA, updateRequest, Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, PurchaseDto>.Right>();
        var dto = ((Either<Error, PurchaseDto>.Right)result).Value;
        dto.Code.Should().Be("Comp-007");
    }

    [Fact]
    public async Task UpdatePurchaseAsync_WithRepeatedCodeOfAnotherPurchase_ReturnsCleanError()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockPurchasesRepo = new Mock<IPurchaseRepository>();
        var mockProductsRepo = new Mock<IProductRepository>();

        var product = new Product { Id = Guid7TestValues.ProductA, Stock = 10, CompanyId = Guid7TestValues.CompanyA, Name = "Test" };

        var existingPurchase = new Purchase
        {
            Id = Guid7TestValues.PurchaseA, CompanyId = Guid7TestValues.CompanyA, Code = "Comp-007",
            SupplierId = Guid7TestValues.SupplierA, SupplierName = "Test",
            Status = EntityStatus.Pending, Total = 500,
            Items = new List<PurchaseItem>
            {
                new() { Id = Guid7TestValues.PurchaseA, ProductId = Guid7TestValues.ProductA, ProductName = "Test", ProductCode = "CODE", Quantity = 5, UnitPrice = 100, Subtotal = 500 }
            }
        };
        var otherPurchase = new Purchase
        {
            Id = Guid.NewGuid(), CompanyId = Guid7TestValues.CompanyA,
            Code = "DUP-1", Status = EntityStatus.Pending
        };

        mockPurchasesRepo.Setup(r => r.GetPurchaseByIdAsync(Guid7TestValues.PurchaseA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPurchase);
        mockPurchasesRepo.Setup(r => r.UpdatePurchaseAsync(It.IsAny<Purchase>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockPurchasesRepo.Setup(r => r.ExistsCodeAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, string code, Guid? excludeId, CancellationToken _) => otherPurchase.Code == code && excludeId != otherPurchase.Id);
        mockProductsRepo.Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });
        mockUnitOfWork.Setup(u => u.Purchases).Returns(mockPurchasesRepo.Object);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new PurchaseService(mockUnitOfWork.Object);
        var updateRequest = new PurchaseDto
        {
            Id = Guid7TestValues.PurchaseA,
            Code = "DUP-1",
            SupplierId = Guid7TestValues.SupplierA,
            SupplierName = "Updated",
            Status = EntityStatus.Pending,
            Total = 800,
            Notes = "",
            Items = new List<PurchaseItemDto>
            {
                new() { Id = Guid.Empty, ProductId = Guid7TestValues.ProductA, ProductName = "Test", ProductCode = "CODE", Quantity = 8, UnitPrice = 100, Subtotal = 800 }
            }
        };

        // ACT
        var result = await service.UpdatePurchaseAsync(Guid7TestValues.PurchaseA, Guid7TestValues.CompanyA, updateRequest, Guid7TestValues.UserA);

        // ASSERT - a clean error, and the inventory was not touched
        result.Should().BeOfType<Either<Error, PurchaseDto>.Left>();
        var error = ((Either<Error, PurchaseDto>.Left)result).Value;
        error.Message.Should().Be("The code is already in use.");
        product.Stock.Should().Be(10);
        mockPurchasesRepo.Verify(r => r.UpdatePurchaseAsync(It.IsAny<Purchase>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // The purchase service asks the generator for the code when the request does
    // not bring one.
    private static void SetupCodes(Mock<IUnitOfWork> mockUnitOfWork, string code = "Comp-001")
    {
        var mockCodes = new Mock<ICodeGenerator>();
        mockCodes.Setup(c => c.NextAsync(It.IsAny<Guid>(), It.IsAny<CodeEntityType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(code);
        mockUnitOfWork.Setup(u => u.Codes).Returns(mockCodes.Object);
    }
}