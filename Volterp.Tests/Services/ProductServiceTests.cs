using EitherWay;
using FluentAssertions;
using Moq;
using Volterp.Application.DTOs;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.DTOs.ProductDtos;
using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Application.Services;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.Domain.ValueObjects;
using Xunit;
using Volterp.Tests.TestHelpers;

namespace Volterp.Tests.Services;

public class ProductServiceTests
{
    [Fact]
    public async Task GetByIdAsync_WhenProductNotFound_ReturnsLeft()
    {
        // The service no longer takes companyId — HasQueryFilter on VolterpDbContext
        // blocks cross-tenant reads at the persistence layer. This test covers the
        // service-level "not found" path. Cross-tenant isolation itself is exercised
        // by integration tests that run against a real DbContext with the filter active.
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockProductsRepo = new Mock<IProductRepository>();
        var mockCategoriesRepo = new Mock<ICategoryRepository>();

        mockProductsRepo.Setup(r => r.GetProductByIdAsync(Guid.NewGuid(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.Categories).Returns(mockCategoriesRepo.Object);

        var service = new ProductService(mockUnitOfWork.Object);

        var result = await service.GetByIdAsync(Guid.NewGuid());

        result.Should().BeOfType<Either<Error, ProductDto?>.Left>();
        var error = ((Either<Error, ProductDto?>.Left)result).Value;
        error.Message.Should().Be("Product not found.");
    }

    [Fact]
    public async Task GetAllAsync_ReturnsPagedResult()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockProductsRepo = new Mock<IProductRepository>();

        var products = new PagedResult<Product>
        {
            Items = new List<Product>
            {
                new() { Id = Guid7TestValues.ProductA, Name = "Product A", CompanyId = Guid7TestValues.CompanyA, Stock = 10, Price = new Money(100, Currency.ARS), Category = "Category A" },
                new() { Id = Guid.NewGuid(), Name = "Product B", CompanyId = Guid7TestValues.CompanyA, Stock = 20, Price = new Money(200, Currency.ARS), Category = "Category B" }
            },
            PageNumber = 1,
            PageSize = 10,
            RowCount = 2,
            PageCount = 1
        };

        mockProductsRepo.Setup(r => r.GetAllProductsAsync(1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(products);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);

        var service = new ProductService(mockUnitOfWork.Object);

        // ACT
        var result = await service.GetAllAsync(1, 10);

        // ASSERT
        result.Items.Should().HaveCount(2);
        result.RowCount.Should().Be(2);
    }

    [Fact]
    public async Task GetByIdAsync_WhenFound_ReturnsProductDto()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockProductsRepo = new Mock<IProductRepository>();
        var mockCategoriesRepo = new Mock<ICategoryRepository>();

        var product = new Product
        {
            Id = Guid7TestValues.ProductA, Name = "Test Product", CompanyId = Guid7TestValues.CompanyA, Stock = 10, Price = new Money(100, Currency.ARS),
            CategoryId = Guid7TestValues.CategoryA, Category = "Category A", Description = "Description", IsActive = true
        };

        var category = new Category { Id = Guid7TestValues.CategoryA, Name = "Category A" };

        mockProductsRepo.Setup(r => r.GetProductByIdAsync(Guid7TestValues.ProductA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        mockCategoriesRepo.Setup(r => r.GetCategoryByIdAsync(Guid7TestValues.CategoryA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.Categories).Returns(mockCategoriesRepo.Object);

        var service = new ProductService(mockUnitOfWork.Object);

        // ACT
        var result = await service.GetByIdAsync(Guid7TestValues.ProductA);

        // ASSERT
        result.Should().BeOfType<Either<Error, ProductDto?>.Right>();
        var dto = ((Either<Error, ProductDto?>.Right)result).Value;
        dto.Should().NotBeNull();
        dto!.Name.Should().Be("Test Product");
        dto.Category.Should().Be("Category A");
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotFound_ThrowsProductNotFoundException()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockProductsRepo = new Mock<IProductRepository>();

        mockProductsRepo.Setup(r => r.GetProductByIdAsync(Guid.NewGuid(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);

        var service = new ProductService(mockUnitOfWork.Object);

        // ACT
        var result = await service.GetByIdAsync(Guid.NewGuid());

        // ASSERT
        result.Should().BeOfType<Either<Error, ProductDto?>.Left>();
        var error = ((Either<Error, ProductDto?>.Left)result).Value;
        error.Message.Should().Be("Product not found.");
    }

    [Fact]
    public async Task CreateAsync_WithInvalidCategoryId_ThrowsCategoryNotFoundException()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockProductsRepo = new Mock<IProductRepository>();
        var mockCategoriesRepo = new Mock<ICategoryRepository>();

        mockCategoriesRepo.Setup(r => r.ExistsCategoryAsync(Guid.NewGuid(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.Categories).Returns(mockCategoriesRepo.Object);

        var service = new ProductService(mockUnitOfWork.Object);
        var request = new CreateProductDto(
            Name: "Test", Category: "Category", Description: "Desc",
            Price: 100, Stock: 10, CategoryId: Guid.NewGuid(), ImageUrl: null
        );

        // ACT
        var result = await service.CreateAsync(request, Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, ProductDto>.Left>();
        var error = ((Either<Error, ProductDto>.Left)result).Value;
        error.Message.Should().Be("Category not found.");
    }

    [Fact]
    public async Task CreateAsync_WithValidInput_CreatesProduct()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockProductsRepo = new Mock<IProductRepository>();
        var mockCategoriesRepo = new Mock<ICategoryRepository>();

        mockCategoriesRepo.Setup(r => r.ExistsCategoryAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        mockProductsRepo.Setup(r => r.AddProductAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product p, CancellationToken ct) => p);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.Categories).Returns(mockCategoriesRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new ProductService(mockUnitOfWork.Object);
        var request = new CreateProductDto(
            Name: "New Product", Category: "Category", Description: "Description",
            Price: 150, Stock: 25, CategoryId: null, ImageUrl: null
        );

        // ACT
        var result = await service.CreateAsync(request, Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, ProductDto>.Right>();
        var dto = ((Either<Error, ProductDto>.Right)result).Value;
        dto.Should().NotBeNull();
        dto.Name.Should().Be("New Product");
        dto.Stock.Should().Be(25);
        mockProductsRepo.Verify(r => r.AddProductAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WhenProductNotFound_ThrowsProductNotFoundException()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockProductsRepo = new Mock<IProductRepository>();

        mockProductsRepo.Setup(r => r.GetProductByIdAsync(Guid.NewGuid(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);

        var service = new ProductService(mockUnitOfWork.Object);
        var request = new UpdateProductDto(
            Name: "Updated", Category: "Category", Description: "Desc",
            Price: 100, Stock: 10, CategoryId: null, IsActive: true, ImageUrl: null
        );

        // ACT
        var result = await service.UpdateAsync(Guid.NewGuid(), request, Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, ProductDto>.Left>();
        var error = ((Either<Error, ProductDto>.Left)result).Value;
        error.Message.Should().Be("Product not found.");
    }

    [Fact]
    public async Task UpdateAsync_WhenCategoryNotFound_ThrowsCategoryNotFoundException()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockProductsRepo = new Mock<IProductRepository>();
        var mockCategoriesRepo = new Mock<ICategoryRepository>();

        var product = new Product { Id = Guid7TestValues.ProductA, Name = "Test", CompanyId = Guid7TestValues.CompanyA, Stock = 10, Price = new Money(100, Currency.ARS) };

        mockProductsRepo.Setup(r => r.GetProductByIdAsync(Guid7TestValues.ProductA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        mockCategoriesRepo.Setup(r => r.ExistsCategoryAsync(Guid.NewGuid(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.Categories).Returns(mockCategoriesRepo.Object);

        var service = new ProductService(mockUnitOfWork.Object);
        var request = new UpdateProductDto(
            Name: "Updated", Category: "Category", Description: "Desc",
            Price: 100, Stock: 10, CategoryId: Guid.NewGuid(), IsActive: true, ImageUrl: null
        );

        // ACT
        var result = await service.UpdateAsync(Guid7TestValues.ProductA, request, Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, ProductDto>.Left>();
        var error = ((Either<Error, ProductDto>.Left)result).Value;
        error.Message.Should().Be("Category not found.");
    }

    [Fact]
    public async Task UpdateAsync_WithValidInput_UpdatesProduct()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockProductsRepo = new Mock<IProductRepository>();
        var mockCategoriesRepo = new Mock<ICategoryRepository>();

        var product = new Product { Id = Guid7TestValues.ProductA, Name = "Old Name", CompanyId = Guid7TestValues.CompanyA, Stock = 10, Price = new Money(100, Currency.ARS), Category = "Old Category" };

        mockProductsRepo.Setup(r => r.GetProductByIdAsync(Guid7TestValues.ProductA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        mockCategoriesRepo.Setup(r => r.ExistsCategoryAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        mockProductsRepo.Setup(r => r.UpdateProductAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.Categories).Returns(mockCategoriesRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new ProductService(mockUnitOfWork.Object);
        var request = new UpdateProductDto(
            Name: "New Name", Category: "New Category", Description: "New Desc",
            Price: 200, Stock: 30, CategoryId: null, IsActive: true, ImageUrl: null
        );

        // ACT
        var result = await service.UpdateAsync(Guid7TestValues.ProductA, request, Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        // ASSERT
        result.Should().NotBeNull();
        product.Name.Should().Be("New Name");
        product.Price.Amount.Should().Be(200);
        mockProductsRepo.Verify(r => r.UpdateProductAsync(product, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithTaxCode_PersistsIt()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockProductsRepo = new Mock<IProductRepository>();
        var mockCategoriesRepo = new Mock<ICategoryRepository>();

        mockCategoriesRepo.Setup(r => r.ExistsCategoryAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        mockProductsRepo.Setup(r => r.AddProductAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product p, CancellationToken ct) => p);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.Categories).Returns(mockCategoriesRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new ProductService(mockUnitOfWork.Object);
        var request = new CreateProductDto(
            Name: "New Product", Category: "Category", Description: "Description",
            Price: 150, Stock: 25, CategoryId: null, ImageUrl: null, TaxCode: TaxCode.Exento
        );

        // ACT
        var result = await service.CreateAsync(request, Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, ProductDto>.Right>();
        var dto = ((Either<Error, ProductDto>.Right)result).Value;
        dto.TaxCode.Should().Be(TaxCode.Exento);
    }

    [Fact]
    public async Task UpdateAsync_WithTaxCode_UpdatesTheProduct()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockProductsRepo = new Mock<IProductRepository>();
        var mockCategoriesRepo = new Mock<ICategoryRepository>();

        var product = new Product { Id = Guid7TestValues.ProductA, Name = "Old Name", CompanyId = Guid7TestValues.CompanyA, Stock = 10, Price = new Money(100, Currency.ARS), Category = "Old Category" };

        mockProductsRepo.Setup(r => r.GetProductByIdAsync(Guid7TestValues.ProductA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        mockCategoriesRepo.Setup(r => r.ExistsCategoryAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        mockProductsRepo.Setup(r => r.UpdateProductAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.Categories).Returns(mockCategoriesRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new ProductService(mockUnitOfWork.Object);
        var request = new UpdateProductDto(
            Name: "New Name", Category: "New Category", Description: "New Desc",
            Price: 200, Stock: 30, CategoryId: null, IsActive: true, ImageUrl: null, TaxCode: TaxCode.Exento
        );

        // ACT
        await service.UpdateAsync(Guid7TestValues.ProductA, request, Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        // ASSERT
        product.TaxCode.Should().Be(TaxCode.Exento);
    }

    [Fact]
    public async Task DeleteAsync_WhenProductNotFound_ThrowsProductNotFoundException()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockProductsRepo = new Mock<IProductRepository>();

        mockProductsRepo.Setup(r => r.GetProductByIdAsync(Guid.NewGuid(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);

        var service = new ProductService(mockUnitOfWork.Object);

        // ACT
        var result = await service.DeleteAsync(Guid.NewGuid(), Guid7TestValues.CompanyA);

        // ASSERT
        result.Should().BeOfType<Either<Error, Unit>.Left>();
        var error = ((Either<Error, Unit>.Left)result).Value;
        error.Message.Should().Be("Product not found.");
    }

    [Fact]
    public async Task DeleteAsync_WhenProductFound_CallsDelete()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockProductsRepo = new Mock<IProductRepository>();

        var product = new Product { Id = Guid7TestValues.ProductA, Name = "Test", CompanyId = Guid7TestValues.CompanyA, Stock = 10, Price = new Money(100, Currency.ARS) };

        mockProductsRepo.Setup(r => r.GetProductByIdAsync(Guid7TestValues.ProductA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        mockProductsRepo.Setup(r => r.DeleteProductAsync(Guid7TestValues.ProductA, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new ProductService(mockUnitOfWork.Object);

        // ACT
        await service.DeleteAsync(Guid7TestValues.ProductA, Guid7TestValues.CompanyA);

        // ASSERT
        mockProductsRepo.Verify(r => r.DeleteProductAsync(Guid7TestValues.ProductA, It.IsAny<CancellationToken>()), Times.Once);
    }
}