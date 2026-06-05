using Volterp.Application.DTOs;


namespace Volterp.Application.Interfaces;

public interface IClientService
{
    Task<PagedResult<ClientDto>> GetAllClientsAsync(int companyId, int pageNumber, int pageSize, CancellationToken ct = default);
    Task<ClientDto?> GetClientByIdAsync(int id, int companyId, CancellationToken ct = default);
    Task<ClientDto> CreateClientAsync(CreateClientDto request, int companyId, CancellationToken ct = default);
    Task<ClientDto> UpdateClientAsync(int id, int companyId, UpdateClientDto request, CancellationToken ct = default);
    Task DeleteClientAsync(int id, int companyId, CancellationToken ct = default);
}
