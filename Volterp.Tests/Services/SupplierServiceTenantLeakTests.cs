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
/// Cycle 1.1 — Regression tests for the wire refactor that closes bugs #2 and #3
/// in <c>SupplierService.cs</c> (create + update paths were persisting
/// <c>request.CompanyId</c> instead of the JWT-derived companyId).
///
/// Cycle 1.0 documented the BUGGY behavior. Cycle 1.1 inverts:
///   - Bug #2 (create): <c>SupplierCreateDto</c> no longer has CompanyId;
///     <c>CreateSupplierAsync</c> persists the JWT-derived param.
///   - Bug #3 (update): <c>UpdateSupplierRequest</c> (new DTO, no CompanyId) is
///     the payload; <c>UpdateSupplierAsync</c> ignores any inbound tenant claim
///     and preserves the existing row's <c>CompanyId</c>.
///
/// The wire-contract guards for the DTO shape live in <c>WireContractTests</c>.
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
    public async Task CreateSupplierAsync_PersistsJwtCompanyId_RegardlessOfRequestBody()
    {
        // ARRANGE — fixed behavior in Cycle 1.1:
        //   SupplierCreateDto.MapTo() copies CompanyId from the request today.
        //   After the wire refactor, SupplierCreateDto no longer has CompanyId.
        //   The service MUST set supplier.CompanyId = companyId (param) after
        //   Project() so the persisted row is bound to the JWT-derived tenant.
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
            // Cycle 1.1: CompanyId property is removed from SupplierCreateDto.
            // Any inbound attempt to set CompanyId must be ignored.
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

        // ASSERT — fixed behavior.
        result.Should().BeOfType<Either<Error, SupplierDto>.Right>();
        capturedSupplier.Should().NotBeNull();
        capturedSupplier!.CompanyId.Should().Be(JwtCompanyId,
            "Cycle 1.1 fix: SupplierService.CreateSupplierAsync must persist the JWT-derived " +
            "companyId (param), not anything the client controls. The request DTO no " +
            "longer carries CompanyId.");

        // Reflection guard — the wire contract.
        typeof(SupplierCreateDto)
            .GetProperty("CompanyId")
            .Should()
            .BeNull("SupplierCreateDto is a request-body DTO; tenant scope comes from JWT only.");

        var dto = ((Either<Error, SupplierDto>.Right)result).Value;
        dto.CompanyId.Should().Be(JwtCompanyId,
            "the persisted tenant must be the JWT-derived one, echoed in the response DTO.");
    }

    [Fact]
    public async Task CreateSupplierAsync_WhenEntityIsPersisted_TheTenantMatchesTheParam()
    {
        // ARRANGE — happy-path control case: the only tenant in the system is the
        // JWT-derived one because the wire cannot set it.
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

        // ACT
        _ = await service.CreateSupplierAsync(request, JwtCompanyId, userId: 7);

        // ASSERT
        capturedSupplier.Should().NotBeNull();
        capturedSupplier!.CompanyId.Should().Be(JwtCompanyId);
    }

    // ───────── Bug #3: UpdateSupplierAsync ─────────

    [Fact]
    public async Task UpdateSupplierAsync_PreservesExistingTenant_RegardlessOfRequestBody()
    {
        // ARRANGE — Cycle 1.1 fix: UpdateSupplierAsync accepts UpdateSupplierRequest,
        // a NEW request-only DTO that has no CompanyId. Even if a malicious caller
        // somehow passes a payload with CompanyId (e.g. by reusing SupplierDto in
        // tests), the service MUST preserve the existing tenant's CompanyId.
        // Existing tenant: JwtCompanyId (100).
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

        var (mockUnitOfWork, mockSuppliersRepo) = BuildUnitOfWork();
        mockSuppliersRepo
            .Setup(r => r.GetSupplierByIdAsync(5, JwtCompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingSupplier);
        mockSuppliersRepo
            .Setup(r => r.UpdateSupplierAsync(It.IsAny<Supplier>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new SupplierService(mockUnitOfWork.Object);

        // UpdateSupplierRequest — no CompanyId, Id is redundant with route param
        // but kept for backward compatibility within the wire payload.
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

        // ACT
        var result = await service.UpdateSupplierAsync(5, JwtCompanyId, updatePayload, userId: 7);

        // ASSERT — fixed behavior.
        result.Should().BeOfType<Either<Error, SupplierDto>.Right>();
        existingSupplier.CompanyId.Should().Be(JwtCompanyId,
            "Cycle 1.1 fix: the existing tenant MUST be preserved. The service must not " +
            "permit a cross-tenant reassignment via the update payload.");

        // Reflection guard — the wire contract.
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
        // ARRANGE — happy-path control case: existing tenant equals JWT tenant.
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

        var (mockUnitOfWork, mockSuppliersRepo) = BuildUnitOfWork();
        mockSuppliersRepo
            .Setup(r => r.GetSupplierByIdAsync(5, JwtCompanyId, It.IsAny<CancellationToken>()))
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

        // ACT
        _ = await service.UpdateSupplierAsync(5, JwtCompanyId, updatePayload, userId: 7);

        // ASSERT
        existingSupplier.CompanyId.Should().Be(JwtCompanyId);
    }
}
