using EitherWay;
using Volterp.Application.DTOs;
using Volterp.Application.DTOs.ClientDtos;
using Volterp.Application.Exceptions.AppErrors;

namespace Volterp.Application.Interfaces;

public interface IClientService
{
    Task<PagedResult<ClientDto>> GetAllClientsAsync(int companyId, int pageNumber, int pageSize, CancellationToken ct = default);
    Task<Either<Error, ClientDto?>> GetClientByIdAsync(int id, int companyId, CancellationToken ct = default);
    Task<Either<Error, ClientDto>> CreateClientAsync(CreateClientDto request, int companyId, CancellationToken ct = default);
    Task<Either<Error, ClientDto>> UpdateClientAsync(int id, int companyId, UpdateClientDto request, CancellationToken ct = default);
    Task<Either<Error, Unit>> DeleteClientAsync(int id, int companyId, CancellationToken ct = default);
}
