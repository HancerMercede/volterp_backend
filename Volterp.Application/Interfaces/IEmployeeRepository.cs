using Volterp.Application.Helpers;
using Volterp.Domain.Entities;

namespace Volterp.Application.Interfaces;

public interface IEmployeeRepository
{
    Task<PagedResult<Employee>> GetAllEmployeesAsync(int pageNumber, int pageSize, CancellationToken ct = default);
    Task<Employee?> GetEmployeeByIdAsync(Guid id, CancellationToken ct = default);
    Task<Employee> AddEmployeeAsync(Employee employee, CancellationToken ct = default);
    Task UpdateEmployeeAsync(Employee employee, CancellationToken ct = default);
    Task DeleteEmployeeAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Tells whether another employee of the same company already uses <paramref name="code"/>.
    /// <paramref name="excludeId"/> skips a row by id so an entity can keep its own code on update.
    /// </summary>
    Task<bool> ExistsCodeAsync(Guid companyId, string code, Guid? excludeId = null, CancellationToken ct = default);
}
