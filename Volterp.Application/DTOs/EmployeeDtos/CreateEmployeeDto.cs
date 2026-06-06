using Volterp.Domain.Entities;
using Volterp.Domain.Enums;

namespace Volterp.Application.DTOs.EmployeeDtos;

public record EmployeeCreateDto(
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
     FirstName = this.FirstName,
     LastName = this.LastName,
     Email = this.Email,
     Phone = this.Phone,
     Position = this.Position,
     Department = this.Department,
     HireDate = this.HireDate,
     Salary = this.Salary,
     Status = this.Status,
     WorkSchedule = this.WorkSchedule,
     ImageUrl = this.ImageUrl,
     AFP = this.AFP,
     ARS = this.ARS,
     NSS = this.NSS,
     Bank = this.Bank,
     AccountNumber = this.AccountNumber
   };
 }
}