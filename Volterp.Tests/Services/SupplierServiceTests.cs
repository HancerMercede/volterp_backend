using EitherWay;
using FluentAssertions;
using Moq;
using Volterp.Application.DTOs.SupplierDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Interfaces;
using Volterp.Application.Services;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Xunit;
using Volterp.Tests.TestHelpers;

namespace Volterp.Tests.Services;

public class SupplierServiceTests
{
    [Fact]
    public async Task CreateSupplierAsync_WithoutCode_UsesTheGeneratedCode()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSuppliersRepo = new Mock<ISupplierRepository>();
        var mockCodes = BuildCodeGenerator("Prov-001");

        Supplier? capturedSupplier = null;
        mockSuppliersRepo.Setup(r => r.AddSupplierAsync(It.IsAny<Supplier>(), It.IsAny<CancellationToken>()))
            .Callback<Supplier, CancellationToken>((s, _) => capturedSupplier = s)
            .ReturnsAsync((Supplier s, CancellationToken _) => s);
        mockUnitOfWork.Setup(u => u.Suppliers).Returns(mockSuppliersRepo.Object);
        mockUnitOfWork.Setup(u => u.Codes).Returns(mockCodes.Object);

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
        var result = await service.CreateSupplierAsync(request, Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, SupplierDto>.Right>();
        var dto = ((Either<Error, SupplierDto>.Right)result).Value;
        dto.Code.Should().Be("Prov-001");
        capturedSupplier.Should().NotBeNull();
        capturedSupplier!.Code.Should().Be("Prov-001");
        mockCodes.Verify(c => c.NextAsync(Guid7TestValues.CompanyA, CodeEntityType.Supplier,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateSupplierAsync_WithCode_KeepsTheGivenCode()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSuppliersRepo = new Mock<ISupplierRepository>();
        var mockCodes = BuildCodeGenerator("Prov-001");

        Supplier? capturedSupplier = null;
        mockSuppliersRepo.Setup(r => r.AddSupplierAsync(It.IsAny<Supplier>(), It.IsAny<CancellationToken>()))
            .Callback<Supplier, CancellationToken>((s, _) => capturedSupplier = s)
            .ReturnsAsync((Supplier s, CancellationToken _) => s);
        mockSuppliersRepo.Setup(r => r.ExistsCodeAsync(Guid7TestValues.CompanyA, "PROV-77", null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        mockUnitOfWork.Setup(u => u.Suppliers).Returns(mockSuppliersRepo.Object);
        mockUnitOfWork.Setup(u => u.Codes).Returns(mockCodes.Object);

        var service = new SupplierService(mockUnitOfWork.Object);
        var request = new SupplierCreateDto
        {
            Name = "Acme Corp",
            Email = "ops@acme.com",
            Phone = "555-1234",
            Address = "123 Main St",
            Category = "Wholesale",
            ContactPerson = "Jane Doe",
            Code = "  PROV-77  "
        };

        // ACT
        var result = await service.CreateSupplierAsync(request, Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, SupplierDto>.Right>();
        var dto = ((Either<Error, SupplierDto>.Right)result).Value;
        dto.Code.Should().Be("PROV-77", "a code typed by the user wins over the generated one.");
        capturedSupplier!.Code.Should().Be("PROV-77");
        mockCodes.Verify(c => c.NextAsync(It.IsAny<Guid>(), It.IsAny<CodeEntityType>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateSupplierAsync_WithDuplicatedCode_ReturnsLeft()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSuppliersRepo = new Mock<ISupplierRepository>();

        mockSuppliersRepo.Setup(r => r.ExistsCodeAsync(Guid7TestValues.CompanyA, "PROV-77", null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        mockUnitOfWork.Setup(u => u.Suppliers).Returns(mockSuppliersRepo.Object);
        mockUnitOfWork.Setup(u => u.Codes).Returns(BuildCodeGenerator("Prov-001").Object);

        var service = new SupplierService(mockUnitOfWork.Object);
        var request = new SupplierCreateDto
        {
            Name = "Acme Corp",
            Email = "ops@acme.com",
            Phone = "555-1234",
            Address = "123 Main St",
            Category = "Wholesale",
            ContactPerson = "Jane Doe",
            Code = "PROV-77"
        };

        // ACT
        var result = await service.CreateSupplierAsync(request, Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, SupplierDto>.Left>();
        var error = ((Either<Error, SupplierDto>.Left)result).Value;
        error.Message.Should().Be("The code is already in use.");
        mockSuppliersRepo.Verify(r => r.AddSupplierAsync(It.IsAny<Supplier>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateSupplierAsync_WithoutCode_KeepsTheCurrentCode()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSuppliersRepo = new Mock<ISupplierRepository>();

        var supplier = new Supplier
        {
            Id = Guid7TestValues.SupplierA,
            CompanyId = Guid7TestValues.CompanyA,
            Name = "Acme Corp",
            Email = "ops@acme.com",
            Phone = "555-1234",
            Address = "123 Main St",
            Category = "Wholesale",
            ContactPerson = "Jane Doe",
            Code = "Prov-001"
        };

        mockSuppliersRepo.Setup(r => r.GetSupplierByIdAsync(Guid7TestValues.SupplierA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(supplier);
        mockSuppliersRepo.Setup(r => r.UpdateSupplierAsync(It.IsAny<Supplier>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockUnitOfWork.Setup(u => u.Suppliers).Returns(mockSuppliersRepo.Object);

        var service = new SupplierService(mockUnitOfWork.Object);
        var request = new UpdateSupplierRequest
        {
            Name = "Acme Corp",
            Email = "ops@acme.com",
            Phone = "555-9999",
            Address = "123 Main St",
            Category = "Wholesale",
            ContactPerson = "Jane Doe"
        };

        // ACT
        var result = await service.UpdateSupplierAsync(Guid7TestValues.SupplierA, Guid7TestValues.CompanyA, request,
            Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, SupplierDto>.Right>();
        supplier.Code.Should().Be("Prov-001", "an empty code keeps the one the supplier already had.");
        mockSuppliersRepo.Verify(r => r.ExistsCodeAsync(It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateSupplierAsync_WithCode_AssignsTheGivenCode()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSuppliersRepo = new Mock<ISupplierRepository>();

        var supplier = new Supplier
        {
            Id = Guid7TestValues.SupplierA,
            CompanyId = Guid7TestValues.CompanyA,
            Name = "Acme Corp",
            Email = "ops@acme.com",
            Phone = "555-1234",
            Address = "123 Main St",
            Category = "Wholesale",
            ContactPerson = "Jane Doe",
            Code = "Prov-001"
        };

        mockSuppliersRepo.Setup(r => r.GetSupplierByIdAsync(Guid7TestValues.SupplierA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(supplier);
        mockSuppliersRepo.Setup(r => r.ExistsCodeAsync(Guid7TestValues.CompanyA, "PROV-99",
                Guid7TestValues.SupplierA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        mockSuppliersRepo.Setup(r => r.UpdateSupplierAsync(It.IsAny<Supplier>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockUnitOfWork.Setup(u => u.Suppliers).Returns(mockSuppliersRepo.Object);

        var service = new SupplierService(mockUnitOfWork.Object);
        var request = new UpdateSupplierRequest
        {
            Name = "Acme Corp",
            Email = "ops@acme.com",
            Phone = "555-9999",
            Address = "123 Main St",
            Category = "Wholesale",
            ContactPerson = "Jane Doe",
            Code = "  PROV-99  "
        };

        // ACT
        var result = await service.UpdateSupplierAsync(Guid7TestValues.SupplierA, Guid7TestValues.CompanyA, request,
            Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, SupplierDto>.Right>();
        supplier.Code.Should().Be("PROV-99");
        var dto = ((Either<Error, SupplierDto>.Right)result).Value;
        dto.Code.Should().Be("PROV-99");
    }

    [Fact]
    public async Task UpdateSupplierAsync_WithDuplicatedCode_ReturnsLeft()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSuppliersRepo = new Mock<ISupplierRepository>();

        var supplier = new Supplier
        {
            Id = Guid7TestValues.SupplierA,
            CompanyId = Guid7TestValues.CompanyA,
            Name = "Acme Corp",
            Email = "ops@acme.com",
            Phone = "555-1234",
            Address = "123 Main St",
            Category = "Wholesale",
            ContactPerson = "Jane Doe",
            Code = "Prov-001"
        };

        mockSuppliersRepo.Setup(r => r.GetSupplierByIdAsync(Guid7TestValues.SupplierA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(supplier);
        mockSuppliersRepo.Setup(r => r.ExistsCodeAsync(Guid7TestValues.CompanyA, "PROV-77",
                Guid7TestValues.SupplierA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        mockUnitOfWork.Setup(u => u.Suppliers).Returns(mockSuppliersRepo.Object);

        var service = new SupplierService(mockUnitOfWork.Object);
        var request = new UpdateSupplierRequest
        {
            Name = "Acme Corp",
            Email = "ops@acme.com",
            Phone = "555-9999",
            Address = "123 Main St",
            Category = "Wholesale",
            ContactPerson = "Jane Doe",
            Code = "PROV-77"
        };

        // ACT
        var result = await service.UpdateSupplierAsync(Guid7TestValues.SupplierA, Guid7TestValues.CompanyA, request,
            Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, SupplierDto>.Left>();
        var error = ((Either<Error, SupplierDto>.Left)result).Value;
        error.Message.Should().Be("The code is already in use.");
        mockSuppliersRepo.Verify(r => r.UpdateSupplierAsync(It.IsAny<Supplier>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static Mock<ICodeGeneratorService> BuildCodeGenerator(string nextCode)
    {
        var mockCodes = new Mock<ICodeGeneratorService>();
        mockCodes.Setup(c => c.NextAsync(It.IsAny<Guid>(), It.IsAny<CodeEntityType>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(nextCode);
        return mockCodes;
    }
}
