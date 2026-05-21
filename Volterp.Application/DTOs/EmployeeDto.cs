using Volterp.Domain.Enums;

namespace Volterp.Application.DTOs;

public record EmployeeDto(
    int Id,
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    string Position,
    string Department,
    DateTime HireDate,
    decimal Salary,
    EntityStatus Status,
    string WorkSchedule,
    string? AFP,
    string? ARS,
    string? NSS,
    string? Bank,
    string? AccountNumber
);