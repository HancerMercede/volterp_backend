using Volterp.Domain.Entities;

namespace Volterp.Application.Interfaces;

public interface ICompanyIssuerRepository : IRepositoryBase<CompanyIssuer>
{
    // The RNCs that can issue for the tenant: its own and the ones of the
    // branches it registered.
    Task<IReadOnlyList<string>> IssuerTaxIdsAsync(Guid companyId, CancellationToken ct = default);

    // The RNC that issues: the one the sale names, or the RNC of the tenant
    // when the sale does not name one. It fails when the RNC does not belong
    // to the tenant.
    Task<string> ResolveIssuerTaxIdAsync(Guid companyId, string? requestedTaxId, CancellationToken ct = default);

    // The tenant that owns an issuer RNC: the one whose RNC it is and the
    // ones that registered it as a branch.
    Task<Company?> CompanyOfIssuerAsync(string taxId, CancellationToken ct = default);

    // The tenants that own an RNC. More than one means the RNC is already
    // claimed twice and nobody can take it.
    Task<IReadOnlyList<Guid>> OwnersOfAsync(string taxId, CancellationToken ct = default);
}
