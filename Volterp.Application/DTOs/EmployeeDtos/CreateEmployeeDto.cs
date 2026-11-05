using Volterp.Domain.Entities;
using Volterp.Domain.Enums;

namespace Volterp.Application.DTOs.EmployeeDtos;

public record CreateEmployeeDto(
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
   // Code is copied as requested; EmployeeService.CreateEmployeeAsync resolves it
   // (user code or generated sequence) and validates its uniqueness.
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