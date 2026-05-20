using Volterp.Application.DTOs;
using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;

namespace Volterp.Application.Services;

public class EmployeeService(IUnitOfWork unitOfWork) : IEmployeeService
{
    public async Task<PagedResult<EmployeeDto>> GetAllEmployeesAsync(int companyId, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var employees = await unitOfWork.Employees.GetAllEmployeesByCompanyAsync(companyId, pageNumber, pageSize, ct);

        return employees.Map(e => new EmployeeDto(
            e.Id, e.FirstName, e.LastName, e.Email, e.Phone, e.Position, e.Department,
            e.HireDate, e.Salary, e.Status, e.WorkSchedule,
            e.AFP, e.ARS, e.NSS, e.Bank, e.AccountNumber,
            e.CreatedAt, e.UpdatedAt, null, null
        ));
    }

    public async Task<EmployeeDto?> GetEmployeeByIdAsync(int id, int companyId, CancellationToken ct = default)
    {
        var employee = await unitOfWork.Employees.GetEmployeeByIdAsync(id, companyId, ct);

        if (employee is null) return null;

        return employee.Map(e => new EmployeeDto(
            e.Id, e.FirstName, e.LastName, e.Email, e.Phone, e.Position, e.Department,
            e.HireDate, e.Salary, e.Status, e.WorkSchedule,
            e.AFP, e.ARS, e.NSS, e.Bank, e.AccountNumber,
            e.CreatedAt, e.UpdatedAt, null, null
        ));
    }

    public async Task<EmployeeDto> CreateEmployeeAsync(EmployeeDto request, int companyId, int userId, CancellationToken ct = default)
    {
        var employee = new Employee
        {
            CompanyId = companyId,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            Phone = request.Phone,
            Position = request.Position,
            Department = request.Department,
            HireDate = request.HireDate,
            Salary = request.Salary,
            Status = request.Status,
            DirectManagerId = null,
            WorkSchedule = request.WorkSchedule,
            AFP = request.AFP,
            ARS = request.ARS,
            NSS = request.NSS,
            Bank = request.Bank,
            AccountNumber = request.AccountNumber,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };

        await unitOfWork.Employees.AddEmployeeAsync(employee, ct);
        await unitOfWork.CommitAsync(ct);

        return employee.Map(e => new EmployeeDto(
            e.Id, e.FirstName, e.LastName, e.Email, e.Phone, e.Position, e.Department,
            e.HireDate, e.Salary, e.Status, e.WorkSchedule,
            e.AFP, e.ARS, e.NSS, e.Bank, e.AccountNumber,
            e.CreatedAt, e.UpdatedAt, e.CreatedBy, e.UpdatedBy
        ));
    }

    public async Task<EmployeeDto> UpdateEmployeeAsync(int id, int companyId, EmployeeDto request, int userId, CancellationToken ct = default)
    {
        var employee = await unitOfWork.Employees.GetEmployeeByIdAsync(id, companyId, ct);

        if (employee is null)
            throw new ArgumentException("Employee not found");

        employee.FirstName = request.FirstName;
        employee.LastName = request.LastName;
        employee.Email = request.Email;
        employee.Phone = request.Phone;
        employee.Position = request.Position;
        employee.Department = request.Department;
        employee.HireDate = request.HireDate;
        employee.Salary = request.Salary;
        employee.Status = request.Status;
        employee.WorkSchedule = request.WorkSchedule;
        employee.AFP = request.AFP;
        employee.ARS = request.ARS;
        employee.NSS = request.NSS;
        employee.Bank = request.Bank;
        employee.AccountNumber = request.AccountNumber;
        employee.UpdatedAt = DateTime.UtcNow;
        employee.UpdatedBy = userId;

        await unitOfWork.Employees.UpdateEmployeeAsync(employee, ct);
        await unitOfWork.CommitAsync(ct);

        return employee.Map(e => new EmployeeDto(
            e.Id, e.FirstName, e.LastName, e.Email, e.Phone, e.Position, e.Department,
            e.HireDate, e.Salary, e.Status, e.WorkSchedule,
            e.AFP, e.ARS, e.NSS, e.Bank, e.AccountNumber,
            e.CreatedAt, e.UpdatedAt, e.CreatedBy, e.UpdatedBy
        ));
    }

    public async Task DeleteEmployeeAsync(int id, int companyId, CancellationToken ct = default)
    {
        var employee = await unitOfWork.Employees.GetEmployeeByIdAsync(id, companyId, ct);

        if (employee is null)
            throw new ArgumentException("Employee not found");

        await unitOfWork.Employees.DeleteEmployeeAsync(id, ct);
        await unitOfWork.CommitAsync(ct);
    }
}