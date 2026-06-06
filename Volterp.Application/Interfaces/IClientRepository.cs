using Volterp.Application.Helpers;
using Volterp.Domain.Entities;

namespace Volterp.Application.Interfaces;

public interface IClientRepository
{
    Task<PagedResult<Client>> GetAllClientsByCompanyAsync(int companyId, int pageNumber, int pageSize, CancellationToken ct = default);
    Task<Client?> GetClientByIdAsync(int id, int companyId, CancellationToken ct = default);
    Task AddClientAsync(Client client, CancellationToken ct = default);
    Task UpdateClientAsync(Client client, CancellationToken ct = default);
    Task DeleteClientAsync(int id, CancellationToken ct = default);
}
