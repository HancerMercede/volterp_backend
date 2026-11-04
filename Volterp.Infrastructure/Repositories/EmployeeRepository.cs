using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Infrastructure.Data;

namespace Volterp.Infrastructure.Repositories;

public class EmployeeRepository(VolterpDbContext context) : RepositoryBase<Employee>(context), IEmployeeRepository
{
    public async Task<PagedResult<Employee>> GetAllEmployeesAsync(int pageNumber, int pageSize,
        CancellationToken ct = default)
        => await GetAllAsync(_ => true, pageNumber, pageSize, ct);

    public async Task<Employee?> GetEmployeeByIdAsync(Guid id, CancellationToken ct = default)
        => await GetByCondictionsAsync(e => e.Id == id, ct);

    public async Task<Employee> AddEmployeeAsync(Employee employee, CancellationToken ct = default)
    {
        await AddAsync(employee, ct);
        return employee;
    }

    public async Task UpdateEmployeeAsync(Employee employee, CancellationToken ct = default)
        => await UpdateAsync(employee, ct);

    public async Task DeleteEmployeeAsync(Guid id, CancellationToken ct = default)
    {
        var employee = await GetByIdAsync(id, ct);
        if (employee is not null)
            await DeleteAsync(employee, ct);
    }
}
