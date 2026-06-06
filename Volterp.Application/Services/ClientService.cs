using Volterp.Application.DTOs;
using Volterp.Application.DTOs.ClientDtos;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;

namespace Volterp.Application.Services;

public class ClientService(IUnitOfWork unitOfWork) : IClientService
{
    public async Task<PagedResult<ClientDto>> GetAllClientsAsync(int companyId, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var clients = await unitOfWork.Clients.GetAllClientsByCompanyAsync(companyId, pageNumber, pageSize, ct);

        return clients.MapTo<Client, ClientDto>();
    }

    public async Task<ClientDto?> GetClientByIdAsync(int id, int companyId, CancellationToken ct = default)
    {
        var client = await unitOfWork.Clients.GetClientByIdAsync(id, companyId, ct);

        if (client is null) return null;
      
        return  client.MapTo<Client, ClientDto>();
    }

    public async Task<ClientDto> CreateClientAsync(CreateClientDto request, int companyId, CancellationToken ct = default)
    {
        var client = request.Project();
        client.CompanyId = companyId;
        
        await unitOfWork.Clients.AddClientAsync(client, ct);
        await unitOfWork.CommitAsync(ct);

        return client.MapTo<Client, ClientDto>();
    }

    public async Task<ClientDto> UpdateClientAsync(int id, int companyId, UpdateClientDto request, CancellationToken ct = default)
    {
        var client = await unitOfWork.Clients.GetClientByIdAsync(id, companyId, ct);

        if (client is null)
            throw new ArgumentException("Client not found");

        client.Apply(c =>
        {
            c.Name = request.Name;
            c.Email = request.Email;
            c.Phone = request.Phone;
            c.Address = request.Address;
            c.IsActive = request.IsActive;
            c.UpdatedAt = DateTime.UtcNow;
            
        });

        await unitOfWork.Clients.UpdateClientAsync(client, ct);
        await unitOfWork.CommitAsync(ct);

        return client.MapTo<Client, ClientDto>();
    }

    public async Task DeleteClientAsync(int id, int companyId, CancellationToken ct = default)
    {
        var client = await unitOfWork.Clients.GetClientByIdAsync(id, companyId, ct);

        if (client is null)
            throw new ArgumentException("Client not found");

        await unitOfWork.Clients.DeleteClientAsync(id, ct);
        await unitOfWork.CommitAsync(ct);
    }
}
