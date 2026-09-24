using EitherWay;
using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using Volterp.Application.DTOs;
using Volterp.Application.DTOs.CompanyDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Application.Services;
using Volterp.Domain.Entities;
using Xunit;
using Volterp.Tests.TestHelpers;

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
                new() { 
                    Id = Guid7TestValues.CompanyA,
                    Name = "Company A",
                    TaxId = "123", 
                    Address = "Address A", 
                    LegalName = "Legal A", 
                    Phone = "123", 
                    Email = "a@test.com" },
                new() { Id = Guid.NewGuid(),
                    Name = "Company B",
                    TaxId = "456",
                    Address = "Address B",
                    LegalName = "Legal B", 
                    Phone = "456", 
                    Email = "b@test.com" }
            },
            PageNumber = 1,
            PageSize = 10,
            RowCount = 2,
            PageCount = 1
        };

        mockCompaniesRepo.Setup(r =>
                r.GetAllCompaniesAsync(1, 10,
                It.IsAny<CancellationToken>()))
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
            Id = Guid7TestValues.CompanyA, Name = "Company A", TaxId = "123", Address = "Address A",
            LegalName = "Legal A", Phone = "123", Email = "a@test.com", IsActive = true
        };

        mockCompaniesRepo.Setup(r => r.GetCompanyByIdAsync(Guid7TestValues.CompanyA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);
        mockUnitOfWork.Setup(u => u.Companies).Returns(mockCompaniesRepo.Object);

        var service = new CompanyService(mockUnitOfWork.Object);

        // ACT
        var result = await service.GetCompanyByIdAsync(Guid7TestValues.CompanyA);
        
        // ASSERT
        result.Should().BeOfType<Either<Error, CompanyDto?>.Right>();
        var companyDto = ((Either<Error, CompanyDto?>.Right)result).Value;
        companyDto!.Name.Should().Be("Company A");
        companyDto.TaxId.Should().Be("123");
    }

    [Fact]
    public async Task GetCompanyByIdAsync_WhenNotFound_ReturnsNull()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockCompaniesRepo = new Mock<ICompanyRepository>();

        mockCompaniesRepo.Setup(r => r.GetCompanyByIdAsync(Guid.NewGuid(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Company?)null);
        mockUnitOfWork.Setup(u => u.Companies).Returns(mockCompaniesRepo.Object);

        var service = new CompanyService(mockUnitOfWork.Object);

        // ACT
        var result = await service.GetCompanyByIdAsync(Guid.NewGuid());

        // ASSERT
        result.Should().BeOfType<Either<Error, CompanyDto?>.Left>();
    }

    [Fact]
    public async Task AddCompanyAsync_WithValidInput_CreatesCompany()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockCompaniesRepo = new Mock<ICompanyRepository>();

        mockCompaniesRepo.Setup(r => r.AddCompanyAsync(It.IsAny<Company>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Company c, CancellationToken ct) => { c.Id = Guid7TestValues.CompanyA; return c; });
        mockUnitOfWork.Setup(u => u.Companies).Returns(mockCompaniesRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new CompanyService(mockUnitOfWork.Object);
        var request = new CreateCompanyDto(
            Name: "New Company", TaxId: "TAX123", LogoUrl: null,
            Address: "123 Main St", LegalName: "New Company LLC",
            Phone: "809-555-1234", Email: "new@company.com"
        );

        // ACT
        var result = await service.AddCompanyAsync(request, Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, CompanyDto?>.Right>();
        var company = ((Either<Error, CompanyDto?>.Right)result).Value;
        company!.Name.Should().Be("New Company");
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
            Id = Guid7TestValues.CompanyA, Name = "Old Name", TaxId = "123", Address = "Old Address",
            LegalName = "Old Legal", Phone = "000", Email = "old@test.com", IsActive = true
        };

        mockCompaniesRepo.Setup(r => r.GetCompanyByIdAsync(Guid7TestValues.CompanyA, It.IsAny<CancellationToken>()))
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
        var result = await service.UpdateCompanyAsync(Guid7TestValues.CompanyA, request, Guid7TestValues.UserA);

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

        mockCompaniesRepo.Setup(r => r.DeleteCompanyAsync(Guid7TestValues.CompanyA, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockUnitOfWork.Setup(u => u.Companies).Returns(mockCompaniesRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new CompanyService(mockUnitOfWork.Object);

        // ACT
        await service.DeleteCompanyAsync(Guid7TestValues.CompanyA);

        // ASSERT
        mockCompaniesRepo.Verify(r => r.DeleteCompanyAsync(Guid7TestValues.CompanyA, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExistsCompanyAsync_WhenExists_ReturnsTrue()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockCompaniesRepo = new Mock<ICompanyRepository>();

        mockCompaniesRepo.Setup(r => r.ExistsAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Volterp.Domain.Entities.Company, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        mockUnitOfWork.Setup(u => u.Companies).Returns(mockCompaniesRepo.Object);

        var service = new CompanyService(mockUnitOfWork.Object);

        // ACT
        var result = await service.ExistsCompanyAsync(Guid7TestValues.CompanyA);

        // ASSERT
        result.Should().BeOfType<Either<Error, bool>.Right>();
        var exists = ((Either<Error, bool>.Right)result).Value;
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsCompanyAsync_WhenNotExists_ReturnsFalse()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockCompaniesRepo = new Mock<ICompanyRepository>();

        mockCompaniesRepo.Setup(r => r.ExistsAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Volterp.Domain.Entities.Company, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        mockUnitOfWork.Setup(u => u.Companies).Returns(mockCompaniesRepo.Object);

        var service = new CompanyService(mockUnitOfWork.Object);

        // ACT
        var result = await service.ExistsCompanyAsync(Guid.NewGuid());

        // ASSERT
        result.Should().BeOfType<Either<Error, bool>.Left>();
    }
}