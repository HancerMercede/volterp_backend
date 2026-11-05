using Volterp.Application.Helpers;
using Volterp.Domain.Entities;

namespace Volterp.Application.Interfaces;

public interface IClientRepository
{
    Task<PagedResult<Client>> GetAllClientsAsync(int pageNumber, int pageSize, CancellationToken ct = default);
    Task<Client?> GetClientByIdAsync(Guid id, CancellationToken ct = default);
    Task AddClientAsync(Client client, CancellationToken ct = default);
    Task UpdateClientAsync(Client client, CancellationToken ct = default);
    Task DeleteClientAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Tells whether another client of the same company already uses <paramref name="code"/>.
    /// <paramref name="excludeId"/> skips a row by id so an entity can keep its own code on update.
    /// </summary>
    Task<bool> ExistsCodeAsync(Guid companyId, string code, Guid? excludeId = null, CancellationToken ct = default);
}
