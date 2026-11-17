using Microsoft.EntityFrameworkCore;
using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Infrastructure.Data;

namespace Volterp.Infrastructure.Repositories;

public class ApiCredentialRepository(VolterpDbContext context)
    : RepositoryBase<ApiCredential>(context), IApiCredentialRepository
{
    // The lookup is by the public selector alone and it ignores the tenant
    // filter: the tenant of the call is what this row is going to resolve,
    // and the filter would hide the row that resolves it.
    public async Task<ApiCredential?> FindByKeyIdAsync(string keyId, CancellationToken ct = default)
        => await Set()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.KeyId == keyId, ct);

    public async Task<PagedResult<ApiCredential>> GetByCompanyAsync(
        Guid companyId, int pageNumber, int pageSize, CancellationToken ct = default)
        => await Set()
            .AsNoTracking()
            .Where(c => c.CompanyId == companyId)
            .OrderByDescending(c => c.CreatedAt)
            .ToPagedResultAsync(pageNumber, pageSize, ct);

    public async Task<int> CommitAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
