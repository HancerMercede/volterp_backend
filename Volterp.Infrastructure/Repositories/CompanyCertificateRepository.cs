using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Volterp.Infrastructure.Repositories;

public class CompanyCertificateRepository(VolterpDbContext context)
    : RepositoryBase<CompanyCertificate>(context), ICompanyCertificateRepository
{
    public async Task<CompanyCertificate?> GetActiveByTaxIdAsync(string taxId, CancellationToken ct = default)
        => await Set()
            .AsNoTracking()
            .Where(c => c.TaxId == taxId
                && c.Status == EntityStatus.Active
                && c.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(c => c.ExpiresAt)
            .FirstOrDefaultAsync(ct);
}
