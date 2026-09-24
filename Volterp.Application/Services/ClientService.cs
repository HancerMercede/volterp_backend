using EitherWay;
using Volterp.Application.DTOs;
using Volterp.Application.DTOs.ClientDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;

namespace Volterp.Application.Services;

public class ClientService(IUnitOfWork unitOfWork) : IClientService
{
    public async Task<PagedResult<ClientDto>> GetAllClientsAsync(Guid companyId, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var clients = await unitOfWork.Clients.GetAllClientsByCompanyAsync(companyId, pageNumber, pageSize, ct);

        return clients.MapTo<Client, ClientDto>();
    }

    public async Task<Either<Error,ClientDto?>> GetClientByIdAsync(Guid id, Guid companyId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, Guid>
            .FromRight(id)
            .Ensure(x => x != Guid.Empty, new Error("id must not be empty."))
            .Ensure(_ => companyId != Guid.Empty, new Error($"companyId must not be empty."))
            .FlatMap(async userId => await unitOfWork.Clients.GetClientByIdAsync(userId, companyId, ct),
                ex => new Error(ex.Message))
            .Ensure(client => client is not null,
                new Error("Client not found."))
            .Map(client => client?.MapTo<Client, ClientDto>())
            .Run();
    }

    public async Task<Either<Error, ClientDto>> CreateClientAsync(CreateClientDto request, Guid companyId, Guid? userId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, CreateClientDto>
            .FromRight(request)
            .Ensure(x=> !string.IsNullOrEmpty(x.Name), new Error("The name is required."))
            .Ensure(x=> !string.IsNullOrEmpty(x.Email), new Error("The email is required."))
            .Ensure(x=> !string.IsNullOrEmpty(x.Phone), new Error("The phone is required."))
            .Ensure(x=> !string.IsNullOrEmpty(x.Address), new Error("The address is required."))
            .Ensure(_ =>  companyId != Guid.Empty, new Error("companyId must not be empty."))
            .Map(x => x.Project())
            .FlatMap(async client =>
            {
                client.CompanyId = companyId;
                if (userId.HasValue) client.SetCreatedBy(userId.Value);
                await unitOfWork.Clients.AddClientAsync(client, ct);
                await unitOfWork.CommitAsync(ct);
                return client;
            }, ex => new Error(ex.Message))
            .Map(client => client.MapTo<Client, ClientDto>())
            .Run();
    }

    public async Task<Either<Error, ClientDto?>> UpdateClientAsync(Guid id, Guid companyId, UpdateClientDto request, Guid? userId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, ClientDto>
            .Try(async () => await unitOfWork.Clients.GetClientByIdAsync(id, companyId, ct))
            .MapLeft(error => new Error(error.Message))
            .Ensure(client => client is not null, new Error("User not found."))
            .Ensure(_ => companyId != Guid.Empty, new Error("company id must not be empty."))
            .Map(client =>
            {
                client.Apply(c =>
                {
                    c?.Name = request.Name;
                    c?.Email = request.Email;
                    c?.Phone = request.Phone;
                    c?.Address = request.Address;
                    c?.IsActive = request.IsActive;
                    c?.UpdatedAt = DateTime.UtcNow;
                    if (userId.HasValue) c?.SetUpdatedBy(userId.Value);
                });
                return client;
            }).FlatMap(async client =>
            {

                await unitOfWork.Clients.UpdateClientAsync(client!, ct);
                await unitOfWork.CommitAsync(ct);
                return client;

            }, ex => new Error(ex.Message))
            .Map(client => client?.MapTo<Client, ClientDto>())
            .Run();
    }

    public async Task<Either<Error, Unit>> DeleteClientAsync(Guid id, Guid companyId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, Guid>
            .FromRight(id)
            .Ensure(x => x != Guid.Empty, new Error("id must not be empty."))
            .Ensure(_ => companyId != Guid.Empty, new Error("company id must not be empty."))
            .FlatMap(async userId => await unitOfWork.Clients.GetClientByIdAsync(userId, companyId, ct),
                error => new Error(error.Message))
            .Ensure(client => client is not null, new Error("User not found."))
            .FlatMap(async client =>
            {
                await unitOfWork.Clients.DeleteClientAsync(client.Id, ct);
                await unitOfWork.CommitAsync(ct);
                return new Unit();
            }, error => new Error(error.Message))
            .Run();
    }
}
