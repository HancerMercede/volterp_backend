using EitherWay;
using Volterp.Application.DTOs;
using Volterp.Application.DTOs.ClientDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.Domain.ValueObjects;

namespace Volterp.Application.Services;

public class ClientService(IUnitOfWork unitOfWork) : IClientService
{
    private const string CodeAlreadyInUseMessage = "The code is already in use.";

    public async Task<PagedResult<ClientDto>> GetAllClientsAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var clients = await unitOfWork.Clients.GetAllClientsAsync(pageNumber, pageSize, ct);

        return clients.MapTo<Client, ClientDto>();
    }

    public async Task<Either<Error,ClientDto?>> GetClientByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await EitherAsync<Error, Guid>
            .FromRight(id)
            .Ensure(x => x != Guid.Empty, new Error("id must not be empty."))
            .FlatMap(async userId => await unitOfWork.Clients.GetClientByIdAsync(userId, ct),
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
            .Ensure(x=>x.Email.IsValidEmail(), new Error("It is not a valid email."))
            .Ensure(x=> !string.IsNullOrEmpty(x.Phone), new Error("The phone is required."))
            .Ensure(x=> !string.IsNullOrEmpty(x.Address), new Error("The address is required."))
            .Ensure(x => x.TaxId is null || Rnc.TryParse(x.TaxId, out _), new Error("The tax id must be a valid RNC or citizen ID."))
            .Map(x => x.Project())
            .Map(client =>
            {
                if (client.TaxId is not null)
                    client.UpdateTaxId(new Rnc(client.TaxId));

                return client;
            })
            .FlatMap(async client =>
            {
                client.CompanyId = companyId;
                if (userId.HasValue) client.SetCreatedBy(userId.Value);

                // The user may bring its own code; otherwise the per-company sequence
                // provides one. A user-provided code must be free inside the company.
                client.Code = await ResolveCodeForCreateAsync(request.Code, companyId, ct);

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
            .Try(async () => await unitOfWork.Clients.GetClientByIdAsync(id, ct))
            .MapLeft(error => new Error(error.Message))
            .Ensure(client => client is not null, new Error("User not found."))
            .Ensure(client => request.TaxId is null || Rnc.TryParse(request.TaxId, out _), new Error("The tax id must be a valid RNC or citizen ID."))
            .FlatMap(async client =>
            {
                // An empty code keeps the current one; a new code must be free
                // inside the company (the row being updated is excluded).
                var resolvedCode = await ResolveCodeForUpdateAsync(request.Code, client!.Code, companyId, client!.Id, ct);

                client.Apply(c =>
                {
                    c?.Name = request.Name;
                    c?.Email = request.Email;
                    c?.Phone = request.Phone;
                    c?.Address = request.Address;
                    c?.IsActive = request.IsActive;
                    c?.ImageUrl = request.ImageUrl;
                    c?.Code = resolvedCode;
                    if (request.TaxId is null)
                        c?.TaxId = null;
                    else
                        c?.UpdateTaxId(new Rnc(request.TaxId));
                    c?.MarkUpdated();
                    if (userId.HasValue) c?.SetUpdatedBy(userId.Value);
                });

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
            .FlatMap(async userId => await unitOfWork.Clients.GetClientByIdAsync(userId, ct),
                error => new Error(error.Message))
            .Ensure(client => client is not null, new Error("User not found."))
            .FlatMap(async _ =>
            {
                await unitOfWork.Clients.DeleteClientAsync(id, ct);
                await unitOfWork.CommitAsync(ct);
                return new Unit();
            }, error => new Error(error.Message))
            .Run();
    }

    /// <summary>
    /// Resolves the code of a new client: the user-provided one (trimmed) when it
    /// comes with a value, otherwise the next code of the company sequence.
    /// </summary>
    private async Task<string> ResolveCodeForCreateAsync(string? requestedCode, Guid companyId, CancellationToken ct)
    {
        var trimmed = requestedCode?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return await unitOfWork.Codes.NextAsync(companyId, CodeEntityType.Client, ct);

        if (await unitOfWork.Clients.ExistsCodeAsync(companyId, trimmed, null, ct))
            throw new Exception(CodeAlreadyInUseMessage);

        return trimmed;
    }

    /// <summary>
    /// Resolves the code of an existing client: the user-provided one (trimmed) when it
    /// comes with a value, otherwise the code the client already has.
    /// </summary>
    private async Task<string> ResolveCodeForUpdateAsync(string? requestedCode, string currentCode, Guid companyId,
        Guid id, CancellationToken ct)
    {
        var trimmed = requestedCode?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return currentCode;

        if (await unitOfWork.Clients.ExistsCodeAsync(companyId, trimmed, id, ct))
            throw new Exception(CodeAlreadyInUseMessage);

        return trimmed;
    }
}
