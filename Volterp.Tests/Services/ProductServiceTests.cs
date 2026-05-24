using FluentAssertions;
using Moq;
using Volterp.Application.DTOs;
using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Application.Services;
using Volterp.Domain.Entities;
using Xunit;

namespace Volterp.Tests.Services;

public class ProductServiceTests
{
    [Fact]
    public async Task GetByIdAsync_WithWrongCompanyId_ReturnsNull()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockProductsRepo = new Mock<IProductRepository>();
        var mockCategoriesRepo = new Mock<ICategoryRepository>();

        var productInCompany1 = new Product { Id = 1, Name = "Test", CompanyId = 1, Stock = 10, Price = 100 };

        mockProductsRepo.Setup(r => r.GetProductByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(productInCompany1);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.Categories).Returns(mockCategoriesRepo.Object);

        var service = new ProductService(mockUnitOfWork.Object);

        // ACT - Request product from company 2
        var result = await service.GetByIdAsync(1, companyId: 2);

        // ASSERT
        result.Should().BeNull();
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
                new() { Id = 1, Name = "Product A", CompanyId = 1, Stock = 10, Price = 100, Category = "Category A" },
                new() { Id = 2, Name = "Product B", CompanyId = 1, Stock = 20, Price = 200, Category = "Category B" }
            },
            PageNumber = 1,
            PageSize = 10,
            RowCount = 2,
            PageCount = 1
        };

        mockProductsRepo.Setup(r => r.GetAllProductsByCompanyAsync(1, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(products);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);

        var service = new ProductService(mockUnitOfWork.Object);

        // ACT
        var result = await service.GetAllAsync(1, 1, 10);

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
            Id = 1, Name = "Test Product", CompanyId = 1, Stock = 10, Price = 100,
            CategoryId = 1, Category = "Category A", Description = "Description", IsActive = true
        };

        var category = new Category { Id = 1, Name = "Category A" };

        mockProductsRepo.Setup(r => r.GetProductByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        mockCategoriesRepo.Setup(r => r.GetCategoryByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.Categories).Returns(mockCategoriesRepo.Object);

        var service = new ProductService(mockUnitOfWork.Object);

        // ACT
        var result = await service.GetByIdAsync(1, companyId: 1);

        // ASSERT
        result.Should().NotBeNull();
        result!.Name.Should().Be("Test Product");
        result.Category.Should().Be("Category A");
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotFound_ReturnsNull()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockProductsRepo = new Mock<IProductRepository>();

        mockProductsRepo.Setup(r => r.GetProductByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);

        var service = new ProductService(mockUnitOfWork.Object);

        // ACT
        var result = await service.GetByIdAsync(999, companyId: 1);

        // ASSERT
        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_WithInvalidCategoryId_ThrowsArgumentException()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockProductsRepo = new Mock<IProductRepository>();
        var mockCategoriesRepo = new Mock<ICategoryRepository>();

        mockCategoriesRepo.Setup(r => r.ExistsCategoryAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.Categories).Returns(mockCategoriesRepo.Object);

        var service = new ProductService(mockUnitOfWork.Object);
        var request = new CreateProductRequest(
            Name: "Test", Category: "Category", Description: "Desc",
            Price: 100, Stock: 10, CategoryId: 999, CompanyId: 1, ImageUrl: null
        );

        // ACT
        var act = () => service.CreateAsync(request, 1);

        // ASSERT
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("Category not found");
    }

    [Fact]
    public async Task CreateAsync_WithValidInput_CreatesProduct()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockProductsRepo = new Mock<IProductRepository>();
        var mockCategoriesRepo = new Mock<ICategoryRepository>();

        mockCategoriesRepo.Setup(r => r.ExistsCategoryAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        mockProductsRepo.Setup(r => r.AddProductAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product p, CancellationToken ct) => p);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.Categories).Returns(mockCategoriesRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new ProductService(mockUnitOfWork.Object);
        var request = new CreateProductRequest(
            Name: "New Product", Category: "Category", Description: "Description",
            Price: 150, Stock: 25, CategoryId: null, CompanyId: 1, ImageUrl: null
        );

        // ACT
        var result = await service.CreateAsync(request, 1);

        // ASSERT
        result.Should().NotBeNull();
        result.Name.Should().Be("New Product");
        result.Stock.Should().Be(25);
        mockProductsRepo.Verify(r => r.AddProductAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WhenProductNotFound_ThrowsArgumentException()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockProductsRepo = new Mock<IProductRepository>();

        mockProductsRepo.Setup(r => r.GetProductByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);

        var service = new ProductService(mockUnitOfWork.Object);
        var request = new UpdateProductRequest(
            Name: "Updated", Category: "Category", Description: "Desc",
            Price: 100, Stock: 10, CategoryId: null, IsActive: true, ImageUrl: null
        );

        // ACT
        var act = () => service.UpdateAsync(999, request, 1);

        // ASSERT
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("Product not found");
    }

    [Fact]
    public async Task UpdateAsync_WhenCategoryNotFound_ThrowsArgumentException()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockProductsRepo = new Mock<IProductRepository>();
        var mockCategoriesRepo = new Mock<ICategoryRepository>();

        var product = new Product { Id = 1, Name = "Test", CompanyId = 1, Stock = 10, Price = 100 };

        mockProductsRepo.Setup(r => r.GetProductByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        mockCategoriesRepo.Setup(r => r.ExistsCategoryAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.Categories).Returns(mockCategoriesRepo.Object);

        var service = new ProductService(mockUnitOfWork.Object);
        var request = new UpdateProductRequest(
            Name: "Updated", Category: "Category", Description: "Desc",
            Price: 100, Stock: 10, CategoryId: 999, IsActive: true, ImageUrl: null
        );

        // ACT
        var act = () => service.UpdateAsync(1, request, 1);

        // ASSERT
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("Category not found");
    }

    [Fact]
    public async Task UpdateAsync_WithValidInput_UpdatesProduct()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockProductsRepo = new Mock<IProductRepository>();
        var mockCategoriesRepo = new Mock<ICategoryRepository>();

        var product = new Product { Id = 1, Name = "Old Name", CompanyId = 1, Stock = 10, Price = 100, Category = "Old Category" };

        mockProductsRepo.Setup(r => r.GetProductByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        mockCategoriesRepo.Setup(r => r.ExistsCategoryAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        mockProductsRepo.Setup(r => r.UpdateProductAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.Categories).Returns(mockCategoriesRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new ProductService(mockUnitOfWork.Object);
        var request = new UpdateProductRequest(
            Name: "New Name", Category: "New Category", Description: "New Desc",
            Price: 200, Stock: 30, CategoryId: null, IsActive: true, ImageUrl: null
        );

        // ACT
        var result = await service.UpdateAsync(1, request, 1);

        // ASSERT
        result.Should().NotBeNull();
        product.Name.Should().Be("New Name");
        product.Price.Should().Be(200);
        mockProductsRepo.Verify(r => r.UpdateProductAsync(product, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WhenProductNotFound_ThrowsArgumentException()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockProductsRepo = new Mock<IProductRepository>();

        mockProductsRepo.Setup(r => r.GetProductByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);

        var service = new ProductService(mockUnitOfWork.Object);

        // ACT
        var act = () => service.DeleteAsync(999, 1);

        // ASSERT
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("Product not found");
    }

    [Fact]
    public async Task DeleteAsync_WhenProductFound_CallsDelete()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockProductsRepo = new Mock<IProductRepository>();

        var product = new Product { Id = 1, Name = "Test", CompanyId = 1, Stock = 10, Price = 100 };

        mockProductsRepo.Setup(r => r.GetProductByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        mockProductsRepo.Setup(r => r.DeleteProductAsync(1, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new ProductService(mockUnitOfWork.Object);

        // ACT
        await service.DeleteAsync(1, 1);

        // ASSERT
        mockProductsRepo.Verify(r => r.DeleteProductAsync(1, It.IsAny<CancellationToken>()), Times.Once);
    }
}