using Volterp.Domain.Entities;

namespace Volterp.Application.Interfaces;

public interface ICompanyCertificateRepository : IRepositoryBase<CompanyCertificate>
{
    /// <summary>
    /// The certificate an issuer signs with: the active one that has not
    /// expired, the one that expires last when there is more than one.
    /// </summary>
    Task<CompanyCertificate?> GetActiveByTaxIdAsync(string taxId, CancellationToken ct = default);
}
