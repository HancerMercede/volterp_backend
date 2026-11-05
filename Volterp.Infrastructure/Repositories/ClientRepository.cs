using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Infrastructure.Data;

namespace Volterp.Infrastructure.Repositories;

public class ClientRepository(VolterpDbContext context) : RepositoryBase<Client>(context), IClientRepository
{
    public async Task<PagedResult<Client>> GetAllClientsAsync(int pageNumber, int pageSize,
        CancellationToken ct = default)
        => await GetAllAsync(_ => true, pageNumber, pageSize, ct);

    public async Task<Client?> GetClientByIdAsync(Guid id, CancellationToken ct = default)
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

    public async Task<bool> ExistsCodeAsync(Guid companyId, string code, Guid? excludeId = null,
        CancellationToken ct = default)
    {
        if (excludeId.HasValue)
            return await ExistsAsync(c => c.CompanyId == companyId && c.Code == code && c.Id != excludeId.Value, ct);

        return await ExistsAsync(c => c.CompanyId == companyId && c.Code == code, ct);
    }
}
