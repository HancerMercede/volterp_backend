using EitherWay;
using Volterp.Application.DTOs.ApiCredentialDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;

namespace Volterp.Application.Services;

public class ApiCredentialService(IApiCredentialRepository credentials) : IApiCredentialService
{
    public async Task<PagedResult<ApiCredentialDto>> GetAllAsync(
        Guid companyId, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var list = await credentials.GetByCompanyAsync(companyId, pageNumber, pageSize, ct);

        return list.MapTo<ApiCredential, ApiCredentialDto>();
    }

    public async Task<Either<Error, IssuedApiCredentialDto>> CreateAsync(
        CreateApiCredentialDto request, Guid companyId, Guid? userId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, CreateApiCredentialDto>
            .FromRight(request)
            .Ensure(r => !string.IsNullOrWhiteSpace(r.Name), new Error("The name is required."))
            .Ensure(r => r.ExpiresAt is null || r.ExpiresAt > DateTime.UtcNow,
                new Error("The expiration date must be in the future."))
            .FlatMap(async r =>
            {
                // The secret is born here and it is handed to the client once:
                // what stays in the database is its hash.
                var issued = ApiCredential.Create(companyId, r.Name.Trim(), r.ExpiresAt?.AsUtc());

                if (userId.HasValue)
                    issued.Credential.SetCreatedBy(userId.Value);

                await credentials.AddAsync(issued.Credential, ct);
                await credentials.CommitAsync(ct);

                return issued;
            }, ex => new Error(ex.Message))
            .Map(issued => new IssuedApiCredentialDto(
                issued.Credential.Id,
                issued.Credential.Name,
                issued.Credential.KeyId,
                issued.Secret,
                issued.Credential.ExpiresAt))
            .Run();
    }

    public async Task<Either<Error, ApiCredentialDto>> RevokeAsync(
        Guid id, Guid companyId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, Guid>
            .FromRight(id)
            .Ensure(x => x != Guid.Empty, new Error("The id must not be empty."))
            .FlatMap(async credentialId => await credentials.GetByCondictionsAsync(
                    c => c.Id == credentialId && c.CompanyId == companyId, ct),
                ex => new Error(ex.Message))
            .Ensure(c => c is not null, new Error("The credential does not exist.", ErrorKind.NotFound))
            .Map(c =>
            {
                var credential = c!;
                credential.Revoke();
                credential.MarkUpdated();
                return credential;
            })
            .FlatMap(async credential =>
            {
                await credentials.UpdateAsync(credential, ct);
                await credentials.CommitAsync(ct);
                return credential;
            }, ex => new Error(ex.Message))
            .Map(c => c.MapTo<ApiCredential, ApiCredentialDto>())
            .Run();
    }
}
