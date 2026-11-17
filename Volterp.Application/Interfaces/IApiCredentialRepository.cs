using Volterp.Domain.Entities;

namespace Volterp.Application.Interfaces;

public interface IApiCredentialRepository : IRepositoryBase<ApiCredential>
{
    // The credential is looked up by its public selector and the lookup
    // ignores the tenant filter: the tenant of the call is what this row is
    // going to resolve.
    Task<ApiCredential?> FindByKeyIdAsync(string keyId, CancellationToken ct = default);

    Task<PagedResult<ApiCredential>> GetByCompanyAsync(
        Guid companyId, int pageNumber, int pageSize, CancellationToken ct = default);

    Task<int> CommitAsync(CancellationToken ct = default);
}
