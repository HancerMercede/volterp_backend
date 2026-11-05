using EitherWay;
using FluentAssertions;
using Moq;
using Volterp.Application.DTOs.EmployeeDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Interfaces;
using Volterp.Application.Services;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.Tests.TestHelpers;
using Xunit;

namespace Volterp.Tests.Services;

public class EmployeeServiceTests
{
    private static readonly Guid EmployeeId = Guid.Parse("0192b9c7-b6a3-7def-8c5e-1f2a3b4c5d77");

    private static CreateEmployeeDto CreateRequest(string? code = null)
        => new(
            FirstName: "Juan",
            LastName: "Perez",
            Email: "juan@example.com",
            Phone: "555-1234",
            Position: "Developer",
            Department: "IT",
            HireDate: new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc),
            Salary: 1000m,
            Status: EntityStatus.Active,
            WorkSchedule: "9-18",
            ImageUrl: null,
            AFP: null,
            ARS: null,
            NSS: null,
            Bank: null,
            AccountNumber: null,
            Code: code);

    private static UpdateEmployeeDto UpdateRequest(string? code = null)
        => new(
            FirstName: "Juan",
            LastName: "Perez",
            Email: "juan@example.com",
            Phone: "555-1234",
            Position: "Developer",
            Department: "IT",
            HireDate: new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc),
            Salary: 1000m,
            Status: EntityStatus.Active,
            WorkSchedule: "9-18",
            ImageUrl: null,
            AFP: null,
            ARS: null,
            NSS: null,
            Bank: null,
            AccountNumber: null,
            Code: code);

    private static Employee ExistingEmployee(string code)
        => new()
        {
            Id = EmployeeId,
            CompanyId = Guid7TestValues.CompanyA,
            Code = code,
            FirstName = "Juan",
            LastName = "Perez",
            Position = "Developer",
            Status = EntityStatus.Active
        };

    // ─────── Friendly codes (feature friendly-ids, T3d) ───────

    [Fact]
    public async Task CreateEmployeeAsync_WithoutCode_UsesTheGeneratedCode()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockEmployees = new Mock<IEmployeeRepository>();
        var mockCodes = new Mock<ICodeGenerator>();

        mockCodes.Setup(c => c.NextAsync(Guid7TestValues.CompanyA, CodeEntityType.Employee, It.IsAny<CancellationToken>()))
            .ReturnsAsync("Empl-001");
        mockEmployees.Setup(r => r.AddEmployeeAsync(It.IsAny<Employee>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Employee e, CancellationToken ct) => e);
        mockUnitOfWork.Setup(u => u.Employees).Returns(mockEmployees.Object);
        mockUnitOfWork.Setup(u => u.Codes).Returns(mockCodes.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new EmployeeService(mockUnitOfWork.Object);

        // ACT
        var result = await service.CreateEmployeeAsync(CreateRequest(), Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, EmployeeDto>.Right>();
        var dto = ((Either<Error, EmployeeDto>.Right)result).Value;
        dto.Code.Should().Be("Empl-001");
        mockCodes.Verify(c => c.NextAsync(Guid7TestValues.CompanyA, CodeEntityType.Employee, It.IsAny<CancellationToken>()), Times.Once);
        mockEmployees.Verify(r => r.AddEmployeeAsync(It.Is<Employee>(e => e.Code == "Empl-001"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateEmployeeAsync_WithUserCode_KeepsTheUserCode()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockEmployees = new Mock<IEmployeeRepository>();
        var mockCodes = new Mock<ICodeGenerator>();

        Employee? persisted = null;
        mockEmployees.Setup(r => r.ExistsCodeAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        mockEmployees.Setup(r => r.AddEmployeeAsync(It.IsAny<Employee>(), It.IsAny<CancellationToken>()))
            .Callback<Employee, CancellationToken>((e, _) => persisted = e)
            .ReturnsAsync((Employee e, CancellationToken ct) => e);
        mockUnitOfWork.Setup(u => u.Employees).Returns(mockEmployees.Object);
        mockUnitOfWork.Setup(u => u.Codes).Returns(mockCodes.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new EmployeeService(mockUnitOfWork.Object);

        // ACT — the code arrives padded, the service trims it
        var result = await service.CreateEmployeeAsync(CreateRequest("  EMP-42  "), Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, EmployeeDto>.Right>();
        var dto = ((Either<Error, EmployeeDto>.Right)result).Value;
        dto.Code.Should().Be("EMP-42");
        persisted.Should().NotBeNull();
        persisted!.Code.Should().Be("EMP-42");
        mockCodes.Verify(c => c.NextAsync(It.IsAny<Guid>(), It.IsAny<CodeEntityType>(), It.IsAny<CancellationToken>()), Times.Never);
        // The uniqueness lookup is scoped to the company of the caller.
        mockEmployees.Verify(r => r.ExistsCodeAsync(Guid7TestValues.CompanyA, "EMP-42", null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateEmployeeAsync_WithDuplicateCode_ReturnsCodeAlreadyInUseError()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockEmployees = new Mock<IEmployeeRepository>();
        var mockCodes = new Mock<ICodeGenerator>();

        mockEmployees.Setup(r => r.ExistsCodeAsync(Guid7TestValues.CompanyA, "EMP-42", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        mockUnitOfWork.Setup(u => u.Employees).Returns(mockEmployees.Object);
        mockUnitOfWork.Setup(u => u.Codes).Returns(mockCodes.Object);

        var service = new EmployeeService(mockUnitOfWork.Object);

        // ACT
        var result = await service.CreateEmployeeAsync(CreateRequest("EMP-42"), Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, EmployeeDto>.Left>();
        var error = ((Either<Error, EmployeeDto>.Left)result).Value;
        error.Message.Should().Be("The code is already in use.");
        mockEmployees.Verify(r => r.AddEmployeeAsync(It.IsAny<Employee>(), It.IsAny<CancellationToken>()), Times.Never);
        mockUnitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        mockCodes.Verify(c => c.NextAsync(It.IsAny<Guid>(), It.IsAny<CodeEntityType>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateEmployeeAsync_WithoutCode_KeepsTheCurrentCode()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockEmployees = new Mock<IEmployeeRepository>();

        var employee = ExistingEmployee("Empl-007");

        mockEmployees.Setup(r => r.GetEmployeeByIdAsync(EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);
        mockEmployees.Setup(r => r.UpdateEmployeeAsync(It.IsAny<Employee>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockUnitOfWork.Setup(u => u.Employees).Returns(mockEmployees.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new EmployeeService(mockUnitOfWork.Object);

        // ACT
        var result = await service.UpdateEmployeeAsync(EmployeeId, Guid7TestValues.CompanyA, UpdateRequest(), Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, EmployeeDto>.Right>();
        var dto = ((Either<Error, EmployeeDto>.Right)result).Value;
        dto.Code.Should().Be("Empl-007");
        employee.Code.Should().Be("Empl-007");
        mockEmployees.Verify(r => r.ExistsCodeAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
        mockEmployees.Verify(r => r.UpdateEmployeeAsync(employee, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateEmployeeAsync_WithNewCode_AssignsTheNewCode()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockEmployees = new Mock<IEmployeeRepository>();

        var employee = ExistingEmployee("Empl-007");

        mockEmployees.Setup(r => r.GetEmployeeByIdAsync(EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);
        mockEmployees.Setup(r => r.ExistsCodeAsync(Guid7TestValues.CompanyA, "EMP-99", EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        mockEmployees.Setup(r => r.UpdateEmployeeAsync(It.IsAny<Employee>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockUnitOfWork.Setup(u => u.Employees).Returns(mockEmployees.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new EmployeeService(mockUnitOfWork.Object);

        // ACT
        var result = await service.UpdateEmployeeAsync(EmployeeId, Guid7TestValues.CompanyA, UpdateRequest("  EMP-99  "), Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, EmployeeDto>.Right>();
        var dto = ((Either<Error, EmployeeDto>.Right)result).Value;
        dto.Code.Should().Be("EMP-99");
        employee.Code.Should().Be("EMP-99");
        // The row being updated is excluded from the uniqueness lookup.
        mockEmployees.Verify(r => r.ExistsCodeAsync(Guid7TestValues.CompanyA, "EMP-99", EmployeeId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateEmployeeAsync_WithDuplicateCode_ReturnsCodeAlreadyInUseError()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockEmployees = new Mock<IEmployeeRepository>();

        var employee = ExistingEmployee("Empl-007");

        mockEmployees.Setup(r => r.GetEmployeeByIdAsync(EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);
        mockEmployees.Setup(r => r.ExistsCodeAsync(Guid7TestValues.CompanyA, "EMP-42", EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        mockUnitOfWork.Setup(u => u.Employees).Returns(mockEmployees.Object);

        var service = new EmployeeService(mockUnitOfWork.Object);

        // ACT
        var result = await service.UpdateEmployeeAsync(EmployeeId, Guid7TestValues.CompanyA, UpdateRequest("EMP-42"), Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, EmployeeDto>.Left>();
        var error = ((Either<Error, EmployeeDto>.Left)result).Value;
        error.Message.Should().Be("The code is already in use.");
        mockEmployees.Verify(r => r.UpdateEmployeeAsync(It.IsAny<Employee>(), It.IsAny<CancellationToken>()), Times.Never);
        mockUnitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        employee.Code.Should().Be("Empl-007", "a rejected update must not change the stored code.");
    }

    // ─────── Pre-existing behaviour (regression cover) ───────

    [Fact]
    public async Task CreateEmployeeAsync_WithoutFirstName_ReturnsTheValidationError()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockEmployees = new Mock<IEmployeeRepository>();
        var mockCodes = new Mock<ICodeGenerator>();

        mockUnitOfWork.Setup(u => u.Employees).Returns(mockEmployees.Object);
        mockUnitOfWork.Setup(u => u.Codes).Returns(mockCodes.Object);

        var service = new EmployeeService(mockUnitOfWork.Object);
        var request = CreateRequest() with { FirstName = "  " };

        // ACT
        var result = await service.CreateEmployeeAsync(request, Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, EmployeeDto>.Left>();
        var error = ((Either<Error, EmployeeDto>.Left)result).Value;
        error.Message.Should().Be("The first name is required.");
        mockEmployees.Verify(r => r.AddEmployeeAsync(It.IsAny<Employee>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateEmployeeAsync_WhenEmployeeNotFound_ReturnsTheNotFoundError()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockEmployees = new Mock<IEmployeeRepository>();

        mockEmployees.Setup(r => r.GetEmployeeByIdAsync(EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Employee?)null);
        mockUnitOfWork.Setup(u => u.Employees).Returns(mockEmployees.Object);

        var service = new EmployeeService(mockUnitOfWork.Object);

        // ACT
        var result = await service.UpdateEmployeeAsync(EmployeeId, Guid7TestValues.CompanyA, UpdateRequest(), Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, EmployeeDto>.Left>();
        var error = ((Either<Error, EmployeeDto>.Left)result).Value;
        error.Message.Should().Be("Employee not found.");
    }
}
