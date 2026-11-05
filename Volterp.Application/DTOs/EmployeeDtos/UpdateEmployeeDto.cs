using Volterp.Domain.Entities;
using Volterp.Domain.Enums;

namespace Volterp.Application.DTOs.EmployeeDtos;

public record UpdateEmployeeDto(
    string FirstName, 
    string LastName, 
    string? Email,
    string? Phone,
    string Position,
    string? Department,
    DateTime HireDate, 
    decimal Salary,
    EntityStatus Status,
    string? WorkSchedule,
    string? ImageUrl,
    string? AFP,
    string? ARS,
    string? NSS,
    string? Bank,
    string? AccountNumber,
    string? Code = null
    ):IMapTo<Employee>
{
    public Employee MapTo()
    {
        // Code is copied as requested; EmployeeService.UpdateEmployeeAsync resolves it
        // (user code or the one the employee already has) and validates its uniqueness.
        return new Employee
        {
            FirstName = FirstName,
            LastName = LastName,
            Email = Email,
            Phone = Phone,
            Position = Position,
            Department = Department,
            HireDate = HireDate,
            Salary = Salary,
            Status = Status,
            WorkSchedule = WorkSchedule,
            ImageUrl = ImageUrl,
            AFP = AFP,
            ARS = ARS,
            NSS = NSS,
            Bank = Bank,
            AccountNumber = AccountNumber,
            Code = Code?.Trim() ?? string.Empty
        };
    }
}