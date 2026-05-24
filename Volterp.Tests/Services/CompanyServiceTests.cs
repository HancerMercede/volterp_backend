using FluentAssertions;
using Moq;
using Volterp.Application.DTOs;
using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Application.Services;
using Volterp.Domain.Entities;
using Xunit;

namespace Volterp.Tests.Services;

public class CompanyServiceTests
{
    [Fact]
    public async Task GetAllCompaniesAsync_ReturnsPagedResult()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockCompaniesRepo = new Mock<ICompanyRepository>();

        var companies = new PagedResult<Company>
        {
            Items = new List<Company>
            {
                new() { Id = 1, Name = "Company A", TaxId = "123", Address = "Address A", LegalName = "Legal A", Phone = "123", Email = "a@test.com" },
                new() { Id = 2, Name = "Company B", TaxId = "456", Address = "Address B", LegalName = "Legal B", Phone = "456", Email = "b@test.com" }
            },
            PageNumber = 1,
            PageSize = 10,
            RowCount = 2,
            PageCount = 1
        };

        mockCompaniesRepo.Setup(r => r.GetAllCompaniesAsync(1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(companies);
        mockUnitOfWork.Setup(u => u.Companies).Returns(mockCompaniesRepo.Object);

        var service = new CompanyService(mockUnitOfWork.Object);

        // ACT
        var result = await service.GetAllCompaniesAsync(1, 10);

        // ASSERT
        result.Items.Should().HaveCount(2);
        result.RowCount.Should().Be(2);
    }

    [Fact]
    public async Task GetCompanyByIdAsync_WhenFound_ReturnsCompanyDto()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockCompaniesRepo = new Mock<ICompanyRepository>();

        var company = new Company
        {
            Id = 1, Name = "Company A", TaxId = "123", Address = "Address A",
            LegalName = "Legal A", Phone = "123", Email = "a@test.com", IsActive = true
        };

        mockCompaniesRepo.Setup(r => r.GetCompanyByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);
        mockUnitOfWork.Setup(u => u.Companies).Returns(mockCompaniesRepo.Object);

        var service = new CompanyService(mockUnitOfWork.Object);

        // ACT
        var result = await service.GetCompanyByIdAsync(1);

        // ASSERT
        result.Should().NotBeNull();
        result!.Name.Should().Be("Company A");
        result.TaxId.Should().Be("123");
    }

    [Fact]
    public async Task GetCompanyByIdAsync_WhenNotFound_ReturnsNull()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockCompaniesRepo = new Mock<ICompanyRepository>();

        mockCompaniesRepo.Setup(r => r.GetCompanyByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Company?)null);
        mockUnitOfWork.Setup(u => u.Companies).Returns(mockCompaniesRepo.Object);

        var service = new CompanyService(mockUnitOfWork.Object);

        // ACT
        var result = await service.GetCompanyByIdAsync(999);

        // ASSERT
        result.Should().BeNull();
    }

    [Fact]
    public async Task AddCompanyAsync_WithValidInput_CreatesCompany()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockCompaniesRepo = new Mock<ICompanyRepository>();

        mockCompaniesRepo.Setup(r => r.AddCompanyAsync(It.IsAny<Company>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Company c, CancellationToken ct) => { c.Id = 1; return c; });
        mockUnitOfWork.Setup(u => u.Companies).Returns(mockCompaniesRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new CompanyService(mockUnitOfWork.Object);
        var request = new CreateCompanyDto(
            Name: "New Company", TaxId: "TAX123", LogoUrl: null,
            Address: "123 Main St", LegalName: "New Company LLC",
            Phone: "809-555-1234", Email: "new@company.com"
        );

        // ACT
        var result = await service.AddCompanyAsync(request);

        // ASSERT
        result.Should().NotBeNull();
        result.Name.Should().Be("New Company");
        mockCompaniesRepo.Verify(r => r.AddCompanyAsync(It.IsAny<Company>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateCompanyAsync_WithValidInput_UpdatesCompany()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockCompaniesRepo = new Mock<ICompanyRepository>();

        var company = new Company
        {
            Id = 1, Name = "Old Name", TaxId = "123", Address = "Old Address",
            LegalName = "Old Legal", Phone = "000", Email = "old@test.com", IsActive = true
        };

        mockCompaniesRepo.Setup(r => r.GetCompanyByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);
        mockCompaniesRepo.Setup(r => r.UpdateCompanyAsync(It.IsAny<Company>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Company c, CancellationToken ct) => c);
        mockUnitOfWork.Setup(u => u.Companies).Returns(mockCompaniesRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new CompanyService(mockUnitOfWork.Object);
        var request = new UpdateCompanyDto(
            Name: "Updated Name", TaxId: "456", LogoUrl: null,
            Address: "New Address", LegalName: "New Legal",
            Phone: "809-555-9999", Email: "updated@test.com"
        );

        // ACT
        var result = await service.UpdateCompanyAsync(1, request);

        // ASSERT
        company.Name.Should().Be("Updated Name");
        company.Email.Should().Be("updated@test.com");
        mockCompaniesRepo.Verify(r => r.UpdateCompanyAsync(company, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteCompanyAsync_CallsDelete()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockCompaniesRepo = new Mock<ICompanyRepository>();

        mockCompaniesRepo.Setup(r => r.DeleteCompanyAsync(1, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockUnitOfWork.Setup(u => u.Companies).Returns(mockCompaniesRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new CompanyService(mockUnitOfWork.Object);

        // ACT
        await service.DeleteCompanyAsync(1);

        // ASSERT
        mockCompaniesRepo.Verify(r => r.DeleteCompanyAsync(1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExistsCompanyAsync_WhenExists_ReturnsTrue()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockCompaniesRepo = new Mock<ICompanyRepository>();

        mockCompaniesRepo.Setup(r => r.ExistsAsync(c => c.Id == 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        mockUnitOfWork.Setup(u => u.Companies).Returns(mockCompaniesRepo.Object);

        var service = new CompanyService(mockUnitOfWork.Object);

        // ACT
        var result = await service.ExistsCompanyAsync(1);

        // ASSERT
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsCompanyAsync_WhenNotExists_ReturnsFalse()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockCompaniesRepo = new Mock<ICompanyRepository>();

        mockCompaniesRepo.Setup(r => r.ExistsAsync(c => c.Id == 999, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        mockUnitOfWork.Setup(u => u.Companies).Returns(mockCompaniesRepo.Object);

        var service = new CompanyService(mockUnitOfWork.Object);

        // ACT
        var result = await service.ExistsCompanyAsync(999);

        // ASSERT
        result.Should().BeFalse();
    }
}