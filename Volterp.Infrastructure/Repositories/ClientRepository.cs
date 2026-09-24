using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Infrastructure.Data;

namespace Volterp.Infrastructure.Repositories;

public class ClientRepository(VolterpDbContext context) : RepositoryBase<Client>(context), IClientRepository
{
    public async Task<PagedResult<Client>> GetAllClientsByCompanyAsync(Guid companyId, int pageNumber, int pageSize,
        CancellationToken ct = default)
        => await GetAllAsync(_ => true, pageNumber, pageSize, ct);

    public async Task<Client?> GetClientByIdAsync(Guid id, Guid companyId, CancellationToken ct = default)
        => await GetByCondictionsAsync(c => c.Id == id, ct);

    public async Task AddClientAsync(Client client, CancellationToken ct = default)
    {
        await AddAsync(client, ct);
    }

    public async Task UpdateClientAsync(Client client, CancellationToken ct = default)
        => await UpdateAsync(client, ct);

    public async Task DeleteClientAsync(Guid id, CancellationToken ct = default)
    {
        var client = await GetByIdAsync(id, ct);
        if (client is not null)
            await DeleteAsync(client, ct);
    }
}
