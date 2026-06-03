using Volterp.Application.DTOs;
using Volterp.Application.Exceptions.Employee;
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
            e.ImageUrl, e.AFP, e.ARS, e.NSS, e.Bank, e.AccountNumber
        ));
    }

    public async Task<EmployeeDto?> GetEmployeeByIdAsync(int id, int companyId, CancellationToken ct = default)
    {
        var employee = await unitOfWork.Employees.GetEmployeeByIdAsync(id, companyId, ct);

        if (employee is null) return null;

        return employee.Map(e => new EmployeeDto(
            e.Id, e.FirstName, e.LastName, e.Email, e.Phone, e.Position, e.Department,
            e.HireDate, e.Salary, e.Status, e.WorkSchedule,
            e.ImageUrl, e.AFP, e.ARS, e.NSS, e.Bank, e.AccountNumber
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
            ImageUrl = request.ImageUrl,
            AFP = request.AFP,
            ARS = request.ARS,
            NSS = request.NSS,
            Bank = request.Bank,
            AccountNumber = request.AccountNumber,
            CreatedAt = DateTime.Now,
            CreatedBy = userId
        };

        await unitOfWork.Employees.AddEmployeeAsync(employee, ct);
        await unitOfWork.CommitAsync(ct);

        return employee.Map(e => new EmployeeDto(
            e.Id, e.FirstName, e.LastName, e.Email, e.Phone, e.Position, e.Department,
            e.HireDate, e.Salary, e.Status, e.WorkSchedule,
            e.ImageUrl, e.AFP, e.ARS, e.NSS, e.Bank, e.AccountNumber
        ));
    }

    public async Task<EmployeeDto> UpdateEmployeeAsync(int id, int companyId, EmployeeDto request, int userId, CancellationToken ct = default)
    {
        var employee = await unitOfWork.Employees.GetEmployeeByIdAsync(id, companyId, ct);

        if (employee is null)
            throw new EmployeeNotFoundException("Employee not found");

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
            x.UpdatedAt = DateTime.Now;
            x.UpdatedBy = userId;
        });

        await unitOfWork.Employees.UpdateEmployeeAsync(employee, ct);
        await unitOfWork.CommitAsync(ct);

        return employee.Map(e => new EmployeeDto(
            e.Id, e.FirstName, e.LastName, e.Email, e.Phone, e.Position, e.Department,
            e.HireDate, e.Salary, e.Status, e.WorkSchedule,
            e.ImageUrl, e.AFP, e.ARS, e.NSS, e.Bank, e.AccountNumber
        ));
    }

    public async Task DeleteEmployeeAsync(int id, int companyId, CancellationToken ct = default)
    {
        var employee = await unitOfWork.Employees.GetEmployeeByIdAsync(id, companyId, ct);

        if (employee is null)
            throw new EmployeeNotFoundException("Employee not found");

        await unitOfWork.Employees.DeleteEmployeeAsync(id, ct);
        await unitOfWork.CommitAsync(ct);
    }
}