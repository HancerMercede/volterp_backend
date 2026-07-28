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
    public async Task<PagedResult<EmployeeDto>> GetAllEmployeesAsync(int companyId, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var employees = await unitOfWork.Employees
            .GetAllEmployeesByCompanyAsync(companyId, pageNumber, pageSize, ct);
        return employees.MapTo<Employee, EmployeeDto>();
    }

    public async Task<Either<Error, EmployeeDto?>> GetEmployeeByIdAsync(int id, int companyId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, int>
            .FromRight(id)
            .Ensure(x => x > 0, new Error("id must be greater than zero."))
            .Ensure(_ => companyId > 0, new Error("companyId must be greater than zero."))
            .FlatMap(async employeeId => await unitOfWork.Employees.GetEmployeeByIdAsync(employeeId, companyId, ct),
                ex => new Error(ex.Message))
            .Map(employee => employee?.MapTo<Employee, EmployeeDto>())
            .Run();
    }

    public async Task<Either<Error, EmployeeDto>> CreateEmployeeAsync(CreateEmployeeDto request, int companyId, int userId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, CreateEmployeeDto>
            .FromRight(request)
            .Ensure(r => !string.IsNullOrWhiteSpace(r.FirstName), new Error("The first name is required."))
            .Ensure(r => !string.IsNullOrWhiteSpace(r.LastName), new Error("The last name is required."))
            .Ensure(_ => companyId > 0, new Error("companyId must be greater than zero."))
            .Map(r => r.Project())
            .Map(employee =>
            {
                employee.CompanyId = companyId;
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

    public async Task<Either<Error, EmployeeDto>> UpdateEmployeeAsync(int id, int companyId, UpdateEmployeeDto request, int userId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, int>
            .FromRight(id)
            .Ensure(x => x > 0, new Error("id must be greater than zero."))
            .Ensure(_ => companyId > 0, new Error("companyId must be greater than zero."))
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
                    x.HireDate = request.HireDate;
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
                    x.UpdatedBy = userId;
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

    public async Task<Either<Error, Unit>> DeleteEmployeeAsync(int id, int companyId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, int>
            .FromRight(id)
            .Ensure(x => x > 0, new Error("id must be greater than zero."))
            .Ensure(_ => companyId > 0, new Error("companyId must be greater than zero."))
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