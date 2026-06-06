using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Infrastructure.Data;

namespace Volterp.Infrastructure.Repositories;

public class EmployeeRepository(VolterpDbContext context) : RepositoryBase<Employee>(context), IEmployeeRepository
{
    public async Task<PagedResult<Employee>> GetAllEmployeesByCompanyAsync(int companyId, int pageNumber, int pageSize,
        CancellationToken ct = default)
        => await GetAllAsync(e => e.CompanyId == companyId, pageNumber, pageSize, ct);

    public async Task<Employee?> GetEmployeeByIdAsync(int id, int companyId, CancellationToken ct = default)
        => await GetByCondictionsAsync(e => e.Id == id && e.CompanyId == companyId, ct);

    public async Task<Employee> AddEmployeeAsync(Employee employee, CancellationToken ct = default)
    {
        await AddAsync(employee, ct);
        return employee;
    }

    public async Task UpdateEmployeeAsync(Employee employee, CancellationToken ct = default)
        => await UpdateAsync(employee, ct);

    public async Task DeleteEmployeeAsync(int id, CancellationToken ct = default)
    {
        var employee = await GetByIdAsync(id, ct);
        if (employee is not null)
            await DeleteAsync(employee, ct);
    }
}