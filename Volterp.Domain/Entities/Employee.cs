using Volterp.Domain.Enums;

namespace Volterp.Domain.Entities;

public class Employee : IAuditEntity
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public DateTime HireDate { get; set; }
    public decimal Salary { get; set; }
    public EntityStatus Status { get; set; } = EntityStatus.Active;
    public int? DirectManagerId { get; set; }
    public string WorkSchedule { get; set; } = string.Empty;
    public string? AFP { get; set; }
    public string? ARS { get; set; }
    public string? NSS { get; set; }
    public string? Bank { get; set; }
    public string? AccountNumber { get; set; }
    public int? CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }

    public Company Company { get; set; } = null!;
}