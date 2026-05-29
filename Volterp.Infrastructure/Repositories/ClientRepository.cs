using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Infrastructure.Data;

namespace Volterp.Infrastructure.Repositories;

public class ClientRepository(VolterpDbContext context) : RepositoryBase<Client>(context), IClientRepository
{
    public async Task<PagedResult<Client>> GetAllClientsByCompanyAsync(int companyId, int pageNumber, int pageSize,
        CancellationToken ct = default)
        => await GetAllAsync(c => c.CompanyId == companyId, pageNumber, pageSize, ct);
    
    public async Task<Client?> GetClientByIdAsync(int id, int companyId, CancellationToken ct = default)
        => await GetByCondictionsAsync(c => c.Id == id && c.CompanyId == companyId, ct);
    
    public async Task AddClientAsync(Client client, CancellationToken ct = default)
    {
        await AddAsync(client, ct);
    }

    public async Task UpdateClientAsync(Client client, CancellationToken ct = default)
        => await UpdateAsync(client, ct);

    public async Task DeleteClientAsync(int id, CancellationToken ct = default)
    {
        var client = await GetByIdAsync(id, ct);
        if (client is not null)
            await DeleteAsync(client, ct);
    }
}
