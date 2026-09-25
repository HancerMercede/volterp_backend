using EitherWay;
using Volterp.Application.DTOs;
using Volterp.Application.DTOs.ClientDtos;
using Volterp.Application.Exceptions.AppErrors;

namespace Volterp.Application.Interfaces;

public interface IClientService
{
    Task<PagedResult<ClientDto>> GetAllClientsAsync(int pageNumber, int pageSize, CancellationToken ct = default);
    Task<Either<Error, ClientDto?>> GetClientByIdAsync(Guid id, CancellationToken ct = default);
    Task<Either<Error, ClientDto>> CreateClientAsync(CreateClientDto request, Guid companyId, Guid? userId, CancellationToken ct = default);
    Task<Either<Error, ClientDto?>> UpdateClientAsync(Guid id, Guid companyId, UpdateClientDto request, Guid? userId, CancellationToken ct = default);
    Task<Either<Error, Unit>> DeleteClientAsync(Guid id, Guid companyId, CancellationToken ct = default);
}
