using EitherWay;
using Volterp.Application.DTOs.ApiCredentialDtos;
using Volterp.Application.Exceptions.AppErrors;

namespace Volterp.Application.Interfaces;

public interface IApiCredentialService
{
    // The company travels by hand: the credentials of a tenant are its own
    // and nothing else.
    Task<PagedResult<ApiCredentialDto>> GetAllAsync(
        Guid companyId, int pageNumber, int pageSize, CancellationToken ct = default);

    // The answer of the creation carries the secret, and it is the only time
    // the secret travels.
    Task<Either<Error, IssuedApiCredentialDto>> CreateAsync(
        CreateApiCredentialDto request, Guid companyId, Guid? userId, CancellationToken ct = default);

    Task<Either<Error, ApiCredentialDto>> RevokeAsync(Guid id, Guid companyId, CancellationToken ct = default);
}
