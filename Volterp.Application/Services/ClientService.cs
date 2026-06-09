using EitherWay;
using Volterp.Application.DTOs;
using Volterp.Application.DTOs.ClientDtos;
using Volterp.Application.Exceptions.AppErrors;
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

    public async Task<Either<Error,ClientDto?>> GetClientByIdAsync(int id, int companyId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, int>
            .FromRight(id)
            .Ensure(x => x > 0, new Error("id must be greater than zero."))
            .Ensure(_ => companyId > 0, new Error($"companyId must be greater than zero."))
            .FlatMap(async userId => await unitOfWork.Clients.GetClientByIdAsync(userId, companyId, ct),
                ex => new Error(ex.Message))
            .Ensure(client => client is not null, 
                new Error("Client not found."))
            .Map(client => client?.MapTo<Client, ClientDto>())
            .Run();
    }

    public async Task<Either<Error, ClientDto>> CreateClientAsync(CreateClientDto request, int companyId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, CreateClientDto>
            .FromRight(request)
            .Ensure(x=> !string.IsNullOrEmpty(x.Name), new Error("The name is required."))
            .Ensure(x=> !string.IsNullOrEmpty(x.Email), new Error("The email is required."))
            .Ensure(x=> !string.IsNullOrEmpty(x.Phone), new Error("The phone is required."))
            .Ensure(x=> !string.IsNullOrEmpty(x.Address), new Error("The address is required."))
            .Ensure(_ =>  companyId > 0, new Error("companyId must be greater than zero."))
            .Map(x => x.Project())
            .FlatMap(async client =>
            {
                client.CompanyId = companyId;
                await unitOfWork.Clients.AddClientAsync(client, ct);
                await unitOfWork.CommitAsync(ct);
                return client;
            }, ex => new Error(ex.Message))
            .Map(client => client.MapTo<Client, ClientDto>())
            .Run();
    }

    public async Task<Either<Error, ClientDto>> UpdateClientAsync(int id, int companyId, UpdateClientDto request, CancellationToken ct = default)
    {
        return await EitherAsync<Error, ClientDto>
            .Try(async () => await unitOfWork.Clients.GetClientByIdAsync(id, companyId, ct))
            .MapLeft(error => new Error(error.Message))
            .Ensure(client => client is not null, new Error("User not found."))
            .Ensure(_ => companyId > 0, new Error("company id must be greater than zero."))
            .Map(client =>
            {
                client.Apply(c =>
                {
                    c.Name = request.Name;
                    c.Email = request.Email;
                    c.Phone = request.Phone;
                    c.Address = request.Address;
                    c.IsActive = request.IsActive;
                    c.UpdatedAt = DateTime.UtcNow;
        
                });
                return client;
            }).FlatMap(async client =>
            {
                await unitOfWork.Clients.UpdateClientAsync(client, ct);
                await unitOfWork.CommitAsync(ct);
                return client;
            }, ex => new Error(ex.Message))
            .Map(client => client.MapTo<Client, ClientDto>())
            .Run();
    }

    public async Task<Either<Error, Unit>> DeleteClientAsync(int id, int companyId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, int>
            .FromRight(id)
            .Ensure(x => x > 0, new Error("id must be greater than zero."))
            .Ensure(_ => companyId > 0, new Error("company id must be greater than zero."))
            .FlatMap(async userId => await unitOfWork.Clients.GetClientByIdAsync(userId, companyId, ct),
                error => new Error(error.Message))
            .Ensure(client => client is not null, new Error("User not found."))
            .FlatMap(async client =>
            {
                await unitOfWork.Clients.AddClientAsync(client, ct);
                await unitOfWork.CommitAsync(ct);
                return new Unit();
            }, error => new Error(error.Message))
            .Run();
    }
}
