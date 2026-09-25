using EitherWay;
using Volterp.Application.DTOs;
using Volterp.Application.DTOs.EmployeeDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;

namespace Volterp.Application.Services;

public class EmployeeService(IUnitOfWork unitOfWork) : IEmployeeService
{
    public async Task<PagedResult<EmployeeDto>> GetAllEmployeesAsync(Guid companyId, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var employees = await unitOfWork.Employees
            .GetAllEmployeesByCompanyAsync(companyId, pageNumber, pageSize, ct);
        return employees.MapTo<Employee, EmployeeDto>();
    }

    public async Task<Either<Error, EmployeeDto?>> GetEmployeeByIdAsync(Guid id, Guid companyId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, Guid>
            .FromRight(id)
            .Ensure(x => x != Guid.Empty, new Error("id must not be empty."))
            .FlatMap(async employeeId => await unitOfWork.Employees.GetEmployeeByIdAsync(employeeId, companyId, ct),
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
            .Map(r => r.Project())
            .Map(employee =>
            {
                employee.CompanyId = companyId;
                employee.SetCreatedBy(userId);
                return employee;
            })
            .FlatMap(async employee =>
            {
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
            .FlatMap(async employeeId => await unitOfWork.Employees.GetEmployeeByIdAsync(employeeId, companyId, ct),
                ex => new Error(ex.Message))
            .Ensure(employee => employee is not null, new Error("Employee not found."))
            .Map(employee =>
            {
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
                    x.UpdatedAt = DateTime.UtcNow;
                    x.SetUpdatedBy(userId);
                });
                return employee;
            })
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
            .FlatMap(async employeeId => await unitOfWork.Employees.GetEmployeeByIdAsync(employeeId, companyId, ct),
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
}
