using Volterp.Application.Helpers;
using Volterp.Domain.Entities;

namespace Volterp.Application.Interfaces;

public interface IEmployeeRepository
{
    Task<PagedResult<Employee>> GetAllEmployeesByCompanyAsync(Guid companyId, int pageNumber, int pageSize, CancellationToken ct = default);
    Task<Employee?> GetEmployeeByIdAsync(Guid id, Guid companyId, CancellationToken ct = default);
    Task<Employee> AddEmployeeAsync(Employee employee, CancellationToken ct = default);
    Task UpdateEmployeeAsync(Employee employee, CancellationToken ct = default);
    Task DeleteEmployeeAsync(Guid id, CancellationToken ct = default);
}
