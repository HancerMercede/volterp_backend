using EitherWay;
using FluentAssertions;
using Moq;
using Volterp.Application.DTOs.SupplierDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Interfaces;
using Volterp.Application.Services;
using Volterp.Domain.Entities;
using Volterp.Tests.TestHelpers;
using Xunit;

namespace Volterp.Tests.Services;

public class SupplierServiceTenantLeakTests
{
    private static readonly Guid JwtCompanyId = Guid7TestValues.CompanyA;
    private static readonly Guid RequestCompanyId = Guid7TestValues.CompanyB;
    private static readonly Guid SupplierId = Guid7TestValues.SupplierA;
    private static readonly Guid JwtUserId = Guid7TestValues.UserA;

    private static (Mock<IUnitOfWork>, Mock<ISupplierRepository>) BuildUnitOfWork()
    {
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSuppliersRepo = new Mock<ISupplierRepository>();
        mockUnitOfWork.Setup(u => u.Suppliers).Returns(mockSuppliersRepo.Object);
        return (mockUnitOfWork, mockSuppliersRepo);
    }

    [Fact]
    public async Task CreateSupplierAsync_PersistsJwtCompanyId_RegardlessOfRequestBody()
    {
        Supplier? capturedSupplier = null;

        var (mockUnitOfWork, mockSuppliersRepo) = BuildUnitOfWork();
        mockSuppliersRepo
            .Setup(r => r.AddSupplierAsync(It.IsAny<Supplier>(), It.IsAny<CancellationToken>()))
            .Callback<Supplier, CancellationToken>((s, _) => capturedSupplier = s)
            .ReturnsAsync((Supplier s, CancellationToken _) => s);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new SupplierService(mockUnitOfWork.Object);

        var request = new SupplierCreateDto
        {
            Name = "Acme Corp",
            Email = "ops@acme.com",
            Phone = "555-1234",
            Address = "123 Main St",
            Category = "Wholesale",
            ContactPerson = "Jane Doe",
            IsActive = true
        };

        var result = await service.CreateSupplierAsync(request, JwtCompanyId, JwtUserId);

        result.Should().BeOfType<Either<Error, SupplierDto>.Right>();
        capturedSupplier.Should().NotBeNull();
        capturedSupplier!.CompanyId.Should().Be(JwtCompanyId,
            "SupplierService.CreateSupplierAsync must persist the JWT-derived " +
            "companyId (param), not anything the client controls.");

        typeof(SupplierCreateDto)
            .GetProperty("CompanyId")
            .Should()
            .BeNull("SupplierCreateDto is a request-body DTO; tenant scope comes from JWT only.");

        var dto = ((Either<Error, SupplierDto>.Right)result).Value;
        dto.CompanyId.Should().Be(JwtCompanyId);
    }

    [Fact]
    public async Task CreateSupplierAsync_WhenEntityIsPersisted_TheTenantMatchesTheParam()
    {
        Supplier? capturedSupplier = null;

        var (mockUnitOfWork, mockSuppliersRepo) = BuildUnitOfWork();
        mockSuppliersRepo
            .Setup(r => r.AddSupplierAsync(It.IsAny<Supplier>(), It.IsAny<CancellationToken>()))
            .Callback<Supplier, CancellationToken>((s, _) => capturedSupplier = s)
            .ReturnsAsync((Supplier s, CancellationToken _) => s);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new SupplierService(mockUnitOfWork.Object);

        var request = new SupplierCreateDto
        {
            Name = "Acme Corp",
            Email = "ops@acme.com",
            Phone = "555-1234",
            Address = "123 Main St",
            Category = "Wholesale",
            ContactPerson = "Jane Doe"
        };

        _ = await service.CreateSupplierAsync(request, JwtCompanyId, JwtUserId);

        capturedSupplier.Should().NotBeNull();
        capturedSupplier!.CompanyId.Should().Be(JwtCompanyId);
    }

    [Fact]
    public async Task UpdateSupplierAsync_PreservesExistingTenant_RegardlessOfRequestBody()
    {
        var existingSupplier = new Supplier
        {
            Id = SupplierId,
            CompanyId = JwtCompanyId,
            Name = "Original Name",
            Email = "old@acme.com",
            Phone = "555-0000",
            Address = "Old Address",
            Category = "Old Cat",
            ContactPerson = "Old Contact",
            IsActive = true
        };

        var (mockUnitOfWork, mockSuppliersRepo) = BuildUnitOfWork();
        mockSuppliersRepo
            .Setup(r => r.GetSupplierByIdAsync(SupplierId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingSupplier);
        mockSuppliersRepo
            .Setup(r => r.UpdateSupplierAsync(It.IsAny<Supplier>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new SupplierService(mockUnitOfWork.Object);

        var updatePayload = new UpdateSupplierRequest
        {
            Name = "Updated Name",
            Email = "new@acme.com",
            Phone = "555-9999",
            Address = "New Address",
            Category = "New Cat",
            ContactPerson = "New Contact",
            IsActive = true
        };

        var result = await service.UpdateSupplierAsync(SupplierId, JwtCompanyId, updatePayload, JwtUserId);

        result.Should().BeOfType<Either<Error, SupplierDto>.Right>();
        existingSupplier.CompanyId.Should().Be(JwtCompanyId,
            "the existing tenant MUST be preserved. The service must not " +
            "permit a cross-tenant reassignment via the update payload.");

        typeof(UpdateSupplierRequest)
            .GetProperty("CompanyId")
            .Should()
            .BeNull("UpdateSupplierRequest is a request-body DTO; tenant scope comes from JWT only.");

        var dto = ((Either<Error, SupplierDto>.Right)result).Value;
        dto.CompanyId.Should().Be(JwtCompanyId);
    }

    [Fact]
    public async Task UpdateSupplierAsync_PreservesTenant_OnHappyPath()
    {
        var existingSupplier = new Supplier
        {
            Id = SupplierId,
            CompanyId = JwtCompanyId,
            Name = "Original Name",
            Email = "old@acme.com",
            Phone = "555-0000",
            Address = "Old Address",
            Category = "Old Cat",
            ContactPerson = "Old Contact",
            IsActive = true
        };

        var (mockUnitOfWork, mockSuppliersRepo) = BuildUnitOfWork();
        mockSuppliersRepo
            .Setup(r => r.GetSupplierByIdAsync(SupplierId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingSupplier);
        mockSuppliersRepo
            .Setup(r => r.UpdateSupplierAsync(It.IsAny<Supplier>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new SupplierService(mockUnitOfWork.Object);

        var updatePayload = new UpdateSupplierRequest
        {
            Name = "Updated Name",
            Email = "new@acme.com",
            Phone = "555-9999",
            Address = "New Address",
            Category = "New Cat",
            ContactPerson = "New Contact"
        };

        _ = await service.UpdateSupplierAsync(SupplierId, JwtCompanyId, updatePayload, JwtUserId);

        existingSupplier.CompanyId.Should().Be(JwtCompanyId);
    }
}