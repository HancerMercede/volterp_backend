using EitherWay;
using Volterp.Application.DTOs.EmployeeDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;

namespace Volterp.Application.Services;

public class EmployeeService(IUnitOfWork unitOfWork) : IEmployeeService
{
    private const string CodeAlreadyInUseMessage = "The code is already in use.";

    public async Task<PagedResult<EmployeeDto>> GetAllEmployeesAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var employees = await unitOfWork.Employees
            .GetAllEmployeesAsync(pageNumber, pageSize, ct);
        return employees.MapTo<Employee, EmployeeDto>();
    }

    public async Task<Either<Error, EmployeeDto?>> GetEmployeeByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await EitherAsync<Error, Guid>
            .FromRight(id)
            .Ensure(x => x != Guid.Empty, new Error("id must not be empty."))
            .FlatMap(async employeeId => await unitOfWork.Employees.GetEmployeeByIdAsync(employeeId, ct),
                ex => new Error(ex.Message))
            .Map(employee => employee?.MapTo<Employee, EmployeeDto>())
            .Run();
    }

    public async Task<Either<Error, EmployeeDto>> CreateEmployeeAsync(CreateEmployeeDto request, Guid companyId, Guid userId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, CreateEmployeeDto>
            .FromRight(request)
            .Ensure(r => !string.IsNullOrWhiteSpace(r.FirstName), new Error("The first name is required."))
            .Ensure(r => !string.IsNullOrWhiteSpace(r.LastName), new Error("The last name is required."))
            .FlatMap(async req =>
            {
                var employee = req.Project();
                employee.CompanyId = companyId;
                employee.SetCreatedBy(userId);
                // The user may bring its own code; otherwise the per-company sequence
                // provides one. A user-provided code must be free inside the company.
                employee.Code = await ResolveCodeForCreateAsync(req.Code, companyId, ct);

                await unitOfWork.Employees.AddEmployeeAsync(employee, ct);
                await unitOfWork.CommitAsync(ct);
                return employee;
            }, ex => new Error(ex.Message))
            .Map(employee => employee.MapTo<Employee, EmployeeDto>())
            .Run();
    }

    public async Task<Either<Error, EmployeeDto>> UpdateEmployeeAsync(Guid id, Guid companyId, UpdateEmployeeDto request, Guid userId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, Guid>
            .FromRight(id)
            .Ensure(x => x != Guid.Empty, new Error("id must not be empty."))
            .FlatMap(async employeeId => await unitOfWork.Employees.GetEmployeeByIdAsync(employeeId, ct),
                ex => new Error(ex.Message))
            .Ensure(employee => employee is not null, new Error("Employee not found."))
            .FlatMap(async employee =>
            {
                // An empty code keeps the current one; a new code must be free
                // inside the company (the row being updated is excluded).
                var resolvedCode = await ResolveCodeForUpdateAsync(request.Code, employee.Code, companyId, id, ct);

                employee.Apply(x =>
                {
                    x.FirstName = request.FirstName;
                    x.LastName = request.LastName;
                    x.Email = request.Email;
                    x.Phone = request.Phone;
                    x.Position = request.Position;
                    x.Department = request.Department;
                    x.HireDate = request.HireDate.AsUtc();
                    x.Salary = request.Salary;
                    x.Status = request.Status;
                    x.WorkSchedule = request.WorkSchedule;
                    x.ImageUrl = request.ImageUrl;
                    x.AFP = request.AFP;
                    x.ARS = request.ARS;
                    x.NSS = request.NSS;
                    x.Bank = request.Bank;
                    x.AccountNumber = request.AccountNumber;
                    x.Code = resolvedCode;
                    x.UpdatedAt = DateTime.UtcNow;
                    x.SetUpdatedBy(userId);
                });
                return employee;
            }, ex => new Error(ex.Message))
            .FlatMap(async employee =>
            {
                await unitOfWork.Employees.UpdateEmployeeAsync(employee, ct);
                await unitOfWork.CommitAsync(ct);
                return employee;
            }, ex => new Error(ex.Message))
            .Map(employee => employee.MapTo<Employee, EmployeeDto>())
            .Run();
    }

    public async Task<Either<Error, Unit>> DeleteEmployeeAsync(Guid id, Guid companyId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, Guid>
            .FromRight(id)
            .Ensure(x => x != Guid.Empty, new Error("id must not be empty."))
            .FlatMap(async employeeId => await unitOfWork.Employees.GetEmployeeByIdAsync(employeeId, ct),
                ex => new Error(ex.Message))
            .Ensure(employee => employee is not null, new Error("Employee not found."))
            .FlatMap(async employee =>
            {
                await unitOfWork.Employees.DeleteEmployeeAsync(employee.Id, ct);
                await unitOfWork.CommitAsync(ct);
                return new Unit();
            }, ex => new Error(ex.Message))
            .Run();
    }

    /// <summary>
    /// Resolves the code of a new employee: the user-provided one (trimmed) when it
    /// comes with a value, otherwise the next code of the company sequence.
    /// </summary>
    private async Task<string> ResolveCodeForCreateAsync(string? requestedCode, Guid companyId, CancellationToken ct)
    {
        var trimmed = requestedCode?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return await unitOfWork.Codes.NextAsync(companyId, CodeEntityType.Employee, ct);

        if (await unitOfWork.Employees.ExistsCodeAsync(companyId, trimmed, null, ct))
            throw new Exception(CodeAlreadyInUseMessage);

        return trimmed;
    }

    /// <summary>
    /// Resolves the code of an existing employee: the user-provided one (trimmed) when it
    /// comes with a value, otherwise the code the employee already has.
    /// </summary>
    private async Task<string> ResolveCodeForUpdateAsync(string? requestedCode, string currentCode, Guid companyId, Guid id, CancellationToken ct)
    {
        var trimmed = requestedCode?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return currentCode;

        if (await unitOfWork.Employees.ExistsCodeAsync(companyId, trimmed, id, ct))
            throw new Exception(CodeAlreadyInUseMessage);

        return trimmed;
    }
}
