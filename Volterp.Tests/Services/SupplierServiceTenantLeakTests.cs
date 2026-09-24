using EitherWay;
using FluentAssertions;
using Moq;
using Volterp.Application.DTOs.SupplierDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Interfaces;
using Volterp.Application.Services;
using Volterp.Domain.Entities;
using Xunit;

namespace Volterp.Tests.Services;

/// <summary>
/// Cycle 1.0 — Characterization tests for cross-tenant bugs in
/// <c>Volterp.Application/Services/SupplierService.cs</c>.
///
/// Bug #2: <c>CreateSupplierAsync</c> validates the JWT-derived companyId but
/// persists <c>request.CompanyId</c> via <c>SupplierCreateDto.MapTo()</c>.
/// Bug #3: <c>UpdateSupplierAsync</c> sets <c>s.CompanyId = request.CompanyId</c>
/// on line 75 of <c>SupplierService.cs</c>, allowing a client to reassign a
/// supplier to a different tenant.
///
/// Both tests pass today against the broken code. When Cycle 1.1 fixes the
/// wire contract and these services, the assertions below must be inverted:
/// the persisted <c>CompanyId</c> must come from the JWT, not from the request.
/// </summary>
public class SupplierServiceTenantLeakTests
{
    private const int JwtCompanyId = 100;
    private const int RequestCompanyId = 200;

    private static (Mock<IUnitOfWork>, Mock<ISupplierRepository>) BuildUnitOfWork()
    {
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSuppliersRepo = new Mock<ISupplierRepository>();
        mockUnitOfWork.Setup(u => u.Suppliers).Returns(mockSuppliersRepo.Object);
        return (mockUnitOfWork, mockSuppliersRepo);
    }

    // ───────── Bug #2: CreateSupplierAsync ─────────

    [Fact]
    public async Task CreateSupplierAsync_PersistsRequestCompanyId_NotJwtCompanyId()
    {
        // ARRANGE
        // The JWT-derived companyId is JwtCompanyId (100). The request carries CompanyId=200.
        // The service uses request.CompanyId for persistence — this is bug #2.
        var (mockUnitOfWork, mockSuppliersRepo) = BuildUnitOfWork();

        Supplier? capturedSupplier = null;
        mockSuppliersRepo
            .Setup(r => r.AddSupplierAsync(It.IsAny<Supplier>(), It.IsAny<CancellationToken>()))
            .Callback<Supplier, CancellationToken>((s, _) => capturedSupplier = s)
            .ReturnsAsync((Supplier s, CancellationToken _) => s);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new SupplierService(mockUnitOfWork.Object);

        var request = new SupplierCreateDto
        {
            CompanyId = RequestCompanyId,
            Name = "Acme Corp",
            Email = "ops@acme.com",
            Phone = "555-1234",
            Address = "123 Main St",
            Category = "Wholesale",
            ContactPerson = "Jane Doe",
            IsActive = true
        };

        // ACT
        var result = await service.CreateSupplierAsync(request, JwtCompanyId, userId: 7);

        // ASSERT — current behavior (bug).
        result.Should().BeOfType<Either<Error, SupplierDto>.Right>();
        capturedSupplier.Should().NotBeNull("the service persists a Supplier via AddSupplierAsync");
        capturedSupplier!.CompanyId.Should().Be(RequestCompanyId,
            "CURRENT BUG: SupplierCreateDto.MapTo() copies CompanyId from the request. " +
            "After Cycle 1.1 this must become JwtCompanyId (100) and SupplierCreateDto must drop CompanyId.");

        // The persisted DTO round-trips the leaked CompanyId.
        var dto = ((Either<Error, SupplierDto>.Right)result).Value;
        dto.CompanyId.Should().Be(RequestCompanyId,
            "CURRENT BUG: response DTO echoes the leaked CompanyId. Will flip in Cycle 1.1.");
    }

    [Fact]
    public async Task CreateSupplierAsync_WhenJwtCompanyIdMatchesRequest_CompanyIdIsPersisted()
    {
        // ARRANGE — control case where the two agree.
        var (mockUnitOfWork, mockSuppliersRepo) = BuildUnitOfWork();

        Supplier? capturedSupplier = null;
        mockSuppliersRepo
            .Setup(r => r.AddSupplierAsync(It.IsAny<Supplier>(), It.IsAny<CancellationToken>()))
            .Callback<Supplier, CancellationToken>((s, _) => capturedSupplier = s)
            .ReturnsAsync((Supplier s, CancellationToken _) => s);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new SupplierService(mockUnitOfWork.Object);

        var request = new SupplierCreateDto
        {
            CompanyId = JwtCompanyId,
            Name = "Acme Corp",
            Email = "ops@acme.com",
            Phone = "555-1234",
            Address = "123 Main St",
            Category = "Wholesale",
            ContactPerson = "Jane Doe"
        };

        // ACT
        _ = await service.CreateSupplierAsync(request, JwtCompanyId, userId: 7);

        // ASSERT
        capturedSupplier.Should().NotBeNull();
        capturedSupplier!.CompanyId.Should().Be(JwtCompanyId);
    }

    // ───────── Bug #3: UpdateSupplierAsync ─────────

    [Fact]
    public async Task UpdateSupplierAsync_ReassignsCompanyIdFromRequest_NotJwtCompanyId()
    {
        // ARRANGE
        // An existing supplier in tenant JwtCompanyId (100). The update request
        // claims CompanyId=200. The service overwrites CompanyId with the request's value.
        var (mockUnitOfWork, mockSuppliersRepo) = BuildUnitOfWork();

        var existingSupplier = new Supplier
        {
            Id = 5,
            CompanyId = JwtCompanyId,
            Name = "Original Name",
            Email = "old@acme.com",
            Phone = "555-0000",
            Address = "Old Address",
            Category = "Old Cat",
            ContactPerson = "Old Contact",
            IsActive = true
        };

        mockSuppliersRepo
            .Setup(r => r.GetSupplierByIdAsync(5, JwtCompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingSupplier);
        mockSuppliersRepo
            .Setup(r => r.UpdateSupplierAsync(It.IsAny<Supplier>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new SupplierService(mockUnitOfWork.Object);

        var updatePayload = new SupplierDto
        {
            Id = 5,
            CompanyId = RequestCompanyId, // client tries to reassign tenant
            Name = "Updated Name",
            Email = "new@acme.com",
            Phone = "555-9999",
            Address = "New Address",
            Category = "New Cat",
            ContactPerson = "New Contact",
            IsActive = true
        };

        // ACT
        var result = await service.UpdateSupplierAsync(5, JwtCompanyId, updatePayload, userId: 7);

        // ASSERT — current behavior (bug).
        result.Should().BeOfType<Either<Error, SupplierDto>.Right>();
        existingSupplier.CompanyId.Should().Be(RequestCompanyId,
            "CURRENT BUG: line 75 of SupplierService.cs assigns s.CompanyId = request.CompanyId. " +
            "After Cycle 1.1 this must remain JwtCompanyId (100) regardless of what the client sends.");

        var dto = ((Either<Error, SupplierDto>.Right)result).Value;
        dto.CompanyId.Should().Be(RequestCompanyId,
            "CURRENT BUG: response DTO reflects the leaked CompanyId. Will flip in Cycle 1.1.");
    }

    [Fact]
    public async Task UpdateSupplierAsync_WhenRequestCompanyIdMatchesJwt_CompanyIdIsPreserved()
    {
        // ARRANGE — control case.
        var (mockUnitOfWork, mockSuppliersRepo) = BuildUnitOfWork();

        var existingSupplier = new Supplier
        {
            Id = 5,
            CompanyId = JwtCompanyId,
            Name = "Original Name",
            Email = "old@acme.com",
            Phone = "555-0000",
            Address = "Old Address",
            Category = "Old Cat",
            ContactPerson = "Old Contact",
            IsActive = true
        };

        mockSuppliersRepo
            .Setup(r => r.GetSupplierByIdAsync(5, JwtCompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingSupplier);
        mockSuppliersRepo
            .Setup(r => r.UpdateSupplierAsync(It.IsAny<Supplier>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new SupplierService(mockUnitOfWork.Object);

        var updatePayload = new SupplierDto
        {
            Id = 5,
            CompanyId = JwtCompanyId,
            Name = "Updated Name",
            Email = "new@acme.com",
            Phone = "555-9999",
            Address = "New Address",
            Category = "New Cat",
            ContactPerson = "New Contact"
        };

        // ACT
        _ = await service.UpdateSupplierAsync(5, JwtCompanyId, updatePayload, userId: 7);

        // ASSERT
        existingSupplier.CompanyId.Should().Be(JwtCompanyId);
    }
}
