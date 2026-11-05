using Volterp.Domain.Entities;
using Volterp.Domain.Enums;

namespace Volterp.Application.DTOs.EmployeeDtos;

public record EmployeeDto : IMapFrom<Employee>
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string Position { get; set; } = string.Empty;
    public string? Department { get; set; }
    public DateTime HireDate { get; set; }
    public decimal Salary { get; set; }
    public EntityStatus Status { get; set; }
    public string? WorkSchedule { get; set; }
    public string? ImageUrl { get; set; }
    public string? AFP { get; set; }
    public string? ARS { get; set; }
    public string? NSS { get; set; }
    public string? Bank { get; set; }
    public string? AccountNumber { get; set; }

    public void MapFrom(Employee source)
    {
        Id = source.Id;
        Code = source.Code;
        FirstName = source.FirstName;
        LastName = source.LastName;
        Email = source.Email;
        Phone = source.Phone;
        Position = source.Position;
        Department = source.Department;
        HireDate = DateTime.SpecifyKind(source.HireDate, DateTimeKind.Utc);
        Salary = source.Salary;
        Status = source.Status;
        WorkSchedule = source.WorkSchedule;
        ImageUrl = source.ImageUrl;
        AFP = source.AFP;
        ARS = source.ARS;
        NSS = source.NSS;
        Bank = source.Bank;
        AccountNumber = source.AccountNumber;
    }
}
