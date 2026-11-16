using EitherWay;
using FluentAssertions;
using Moq;
using Volterp.Application.DTOs.CategoryDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Interfaces;
using Volterp.Application.Services;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Xunit;
using Volterp.Tests.TestHelpers;

namespace Volterp.Tests.Services;

public class CategoryServiceTests
{
    [Fact]
    public async Task GetAllAsync_ReturnsPagedResult()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockCategoriesRepo = new Mock<ICategoryRepository>();

        var categories = new PagedResult<Category>
        {
            Items = new List<Category>
            {
                new() { Id = Guid7TestValues.CategoryA, Name = "Category A", CompanyId = Guid7TestValues.CompanyA, Code = "Cat-001" },
                new() { Id = Guid.NewGuid(), Name = "Category B", CompanyId = Guid7TestValues.CompanyA, Code = "Cat-002" }
            },
            PageNumber = 1,
            PageSize = 10,
            RowCount = 2,
            PageCount = 1
        };

        mockCategoriesRepo.Setup(r => r.GetAllCategoriesAsync(1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(categories);
        mockUnitOfWork.Setup(u => u.Categories).Returns(mockCategoriesRepo.Object);

        var service = new CategoryService(mockUnitOfWork.Object);

        // ACT
        var result = await service.GetAllAsync(1, 10);

        // ASSERT
        result.Items.Should().HaveCount(2);
        result.RowCount.Should().Be(2);
        result.Items.First().Code.Should().Be("Cat-001");
    }

    [Fact]
    public async Task CreateAsync_WithValidInput_CreatesCategory()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockCategoriesRepo = new Mock<ICategoryRepository>();
        var mockCodes = new Mock<ICodeGeneratorService>();

        Category? persisted = null;
        mockCategoriesRepo.Setup(r => r.AddCategoryAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>()))
            .Callback<Category, CancellationToken>((c, _) => persisted = c)
            .ReturnsAsync((Category c, CancellationToken ct) => c);
        mockCodes.Setup(c => c.NextAsync(Guid7TestValues.CompanyA, CodeEntityType.Category, It.IsAny<CancellationToken>()))
            .ReturnsAsync("Cat-001");
        mockUnitOfWork.Setup(u => u.Categories).Returns(mockCategoriesRepo.Object);
        mockUnitOfWork.Setup(u => u.Codes).Returns(mockCodes.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new CategoryService(mockUnitOfWork.Object);
        var request = new CreateCategoryRequest(Name: "New Category", Description: "Description");

        // ACT
        var result = await service.CreateAsync(request, Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, CategoryDto>.Right>();
        var dto = ((Either<Error, CategoryDto>.Right)result).Value;
        dto.Name.Should().Be("New Category");
        dto.CompanyId.Should().Be(Guid7TestValues.CompanyA);
        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be("New Category");
        mockCategoriesRepo.Verify(r => r.AddCategoryAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WithValidInput_UpdatesTheCategory()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockCategoriesRepo = new Mock<ICategoryRepository>();

        var category = new Category
        {
            Id = Guid7TestValues.CategoryA, Name = "Old Name", CompanyId = Guid7TestValues.CompanyA, Code = "Cat-001"
        };

        mockCategoriesRepo.Setup(r => r.GetCategoryByIdAsync(Guid7TestValues.CategoryA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);
        mockCategoriesRepo.Setup(r => r.UpdateCategoryAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockUnitOfWork.Setup(u => u.Categories).Returns(mockCategoriesRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new CategoryService(mockUnitOfWork.Object);
        var request = new UpdateCategoryRequest(Name: "New Name", Description: "New Description", IsActive: false);

        // ACT
        var result = await service.UpdateAsync(Guid7TestValues.CategoryA, request, Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, CategoryDto>.Right>();
        category.Name.Should().Be("New Name");
        category.Description.Should().Be("New Description");
        category.IsActive.Should().BeFalse();
        mockCategoriesRepo.Verify(r => r.UpdateCategoryAsync(category, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─────── Friendly codes (feature friendly-ids, T3a) ───────

    [Fact]
    public async Task CreateAsync_WithoutCode_UsesTheGeneratedCode()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockCategoriesRepo = new Mock<ICategoryRepository>();
        var mockCodes = new Mock<ICodeGeneratorService>();

        mockCategoriesRepo.Setup(r => r.AddCategoryAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category c, CancellationToken ct) => c);
        mockCodes.Setup(c => c.NextAsync(Guid7TestValues.CompanyA, CodeEntityType.Category, It.IsAny<CancellationToken>()))
            .ReturnsAsync("Cat-001");
        mockUnitOfWork.Setup(u => u.Categories).Returns(mockCategoriesRepo.Object);
        mockUnitOfWork.Setup(u => u.Codes).Returns(mockCodes.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new CategoryService(mockUnitOfWork.Object);
        var request = new CreateCategoryRequest(Name: "New Category", Description: "Description");

        // ACT
        var result = await service.CreateAsync(request, Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, CategoryDto>.Right>();
        var dto = ((Either<Error, CategoryDto>.Right)result).Value;
        dto.Code.Should().Be("Cat-001");
        mockCodes.Verify(c => c.NextAsync(Guid7TestValues.CompanyA, CodeEntityType.Category, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithUserCode_KeepsTheUserCode()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockCategoriesRepo = new Mock<ICategoryRepository>();
        var mockCodes = new Mock<ICodeGeneratorService>();

        Category? persisted = null;
        mockCategoriesRepo.Setup(r => r.ExistsCodeAsync(Guid7TestValues.CompanyA, "CAT-77", It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        mockCategoriesRepo.Setup(r => r.AddCategoryAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>()))
            .Callback<Category, CancellationToken>((c, _) => persisted = c)
            .ReturnsAsync((Category c, CancellationToken ct) => c);
        mockUnitOfWork.Setup(u => u.Categories).Returns(mockCategoriesRepo.Object);
        mockUnitOfWork.Setup(u => u.Codes).Returns(mockCodes.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new CategoryService(mockUnitOfWork.Object);
        var request = new CreateCategoryRequest(Name: "New Category", Description: "Description", Code: " CAT-77 ");

        // ACT
        var result = await service.CreateAsync(request, Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, CategoryDto>.Right>();
        var dto = ((Either<Error, CategoryDto>.Right)result).Value;
        dto.Code.Should().Be("CAT-77");
        persisted.Should().NotBeNull();
        persisted!.Code.Should().Be("CAT-77");
        mockCodes.Verify(c => c.NextAsync(It.IsAny<Guid>(), It.IsAny<CodeEntityType>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateCode_ReturnsCodeAlreadyInUseError()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockCategoriesRepo = new Mock<ICategoryRepository>();
        var mockCodes = new Mock<ICodeGeneratorService>();

        mockCategoriesRepo.Setup(r => r.ExistsCodeAsync(Guid7TestValues.CompanyA, "CAT-77", It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        mockUnitOfWork.Setup(u => u.Categories).Returns(mockCategoriesRepo.Object);
        mockUnitOfWork.Setup(u => u.Codes).Returns(mockCodes.Object);

        var service = new CategoryService(mockUnitOfWork.Object);
        var request = new CreateCategoryRequest(Name: "New Category", Description: "Description", Code: "CAT-77");

        // ACT
        var result = await service.CreateAsync(request, Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, CategoryDto>.Left>();
        var error = ((Either<Error, CategoryDto>.Left)result).Value;
        error.Message.Should().Be("The code is already in use.");
        mockCategoriesRepo.Verify(r => r.AddCategoryAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>()), Times.Never);
        mockUnitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WithoutCode_KeepsTheCurrentCode()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockCategoriesRepo = new Mock<ICategoryRepository>();

        var category = new Category
        {
            Id = Guid7TestValues.CategoryA, Name = "Old Name", CompanyId = Guid7TestValues.CompanyA, Code = "Cat-001"
        };

        mockCategoriesRepo.Setup(r => r.GetCategoryByIdAsync(Guid7TestValues.CategoryA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);
        mockCategoriesRepo.Setup(r => r.UpdateCategoryAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockUnitOfWork.Setup(u => u.Categories).Returns(mockCategoriesRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new CategoryService(mockUnitOfWork.Object);
        var request = new UpdateCategoryRequest(Name: "New Name", Description: "New Description", IsActive: true);

        // ACT
        var result = await service.UpdateAsync(Guid7TestValues.CategoryA, request, Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, CategoryDto>.Right>();
        var dto = ((Either<Error, CategoryDto>.Right)result).Value;
        dto.Code.Should().Be("Cat-001");
        category.Code.Should().Be("Cat-001");
        mockCategoriesRepo.Verify(r => r.ExistsCodeAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WithNewCode_AssignsTheNewCode()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockCategoriesRepo = new Mock<ICategoryRepository>();

        var category = new Category
        {
            Id = Guid7TestValues.CategoryA, Name = "Old Name", CompanyId = Guid7TestValues.CompanyA, Code = "Cat-001"
        };

        mockCategoriesRepo.Setup(r => r.GetCategoryByIdAsync(Guid7TestValues.CategoryA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);
        mockCategoriesRepo.Setup(r => r.ExistsCodeAsync(Guid7TestValues.CompanyA, "CAT-77", Guid7TestValues.CategoryA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        mockCategoriesRepo.Setup(r => r.UpdateCategoryAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockUnitOfWork.Setup(u => u.Categories).Returns(mockCategoriesRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new CategoryService(mockUnitOfWork.Object);
        var request = new UpdateCategoryRequest(Name: "New Name", Description: "New Description", IsActive: true, Code: " CAT-77 ");

        // ACT
        var result = await service.UpdateAsync(Guid7TestValues.CategoryA, request, Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, CategoryDto>.Right>();
        var dto = ((Either<Error, CategoryDto>.Right)result).Value;
        dto.Code.Should().Be("CAT-77");
        category.Code.Should().Be("CAT-77");
        // The row being updated is excluded from the uniqueness lookup.
        mockCategoriesRepo.Verify(r => r.ExistsCodeAsync(Guid7TestValues.CompanyA, "CAT-77", Guid7TestValues.CategoryA, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WithDuplicateCode_ReturnsCodeAlreadyInUseError()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockCategoriesRepo = new Mock<ICategoryRepository>();

        var category = new Category
        {
            Id = Guid7TestValues.CategoryA, Name = "Old Name", CompanyId = Guid7TestValues.CompanyA, Code = "Cat-001"
        };

        mockCategoriesRepo.Setup(r => r.GetCategoryByIdAsync(Guid7TestValues.CategoryA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);
        mockCategoriesRepo.Setup(r => r.ExistsCodeAsync(Guid7TestValues.CompanyA, "CAT-77", Guid7TestValues.CategoryA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        mockUnitOfWork.Setup(u => u.Categories).Returns(mockCategoriesRepo.Object);

        var service = new CategoryService(mockUnitOfWork.Object);
        var request = new UpdateCategoryRequest(Name: "New Name", Description: "New Description", IsActive: true, Code: "CAT-77");

        // ACT
        var result = await service.UpdateAsync(Guid7TestValues.CategoryA, request, Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, CategoryDto>.Left>();
        var error = ((Either<Error, CategoryDto>.Left)result).Value;
        error.Message.Should().Be("The code is already in use.");
        mockCategoriesRepo.Verify(r => r.UpdateCategoryAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>()), Times.Never);
        mockUnitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        category.Code.Should().Be("Cat-001", "a rejected update must not change the stored code.");
    }
}
