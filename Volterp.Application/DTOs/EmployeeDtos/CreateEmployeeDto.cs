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
 string? AccountNumber
):IMapTo<Employee>
{
 public Employee MapTo()
 {
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
     AccountNumber = AccountNumber
   };
 }
}