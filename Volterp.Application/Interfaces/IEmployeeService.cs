using Volterp.Application.DTOs;
using Volterp.Application.Helpers;

namespace Volterp.Application.Interfaces;

public interface IEmployeeService
{
    Task<PagedResult<EmployeeDto>> GetAllEmployeesAsync(int companyId, int pageNumber, int pageSize, CancellationToken ct = default);
    Task<EmployeeDto?> GetEmployeeByIdAsync(int id, int companyId, CancellationToken ct = default);
    Task<EmployeeDto> CreateEmployeeAsync(EmployeeDto request, int companyId, int userId, CancellationToken ct = default);
    Task<EmployeeDto> UpdateEmployeeAsync(int id, int companyId, EmployeeDto request, int userId, CancellationToken ct = default);
    Task DeleteEmployeeAsync(int id, int companyId, CancellationToken ct = default);
}