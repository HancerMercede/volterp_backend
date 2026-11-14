using Microsoft.EntityFrameworkCore;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.ElectronicInvoicing;
using Volterp.Infrastructure.Data;

namespace Volterp.Infrastructure.Repositories;

public class ElectronicInvoiceRepository(VolterpDbContext context)
    : RepositoryBase<ElectronicInvoice>(context), IElectronicInvoiceRepository
{
    public async Task<ElectronicInvoice?> GetBySaleIdAsync(Guid saleId, CancellationToken ct = default)
        => await Set()
            .AsNoTracking()
            .Where(e => e.SaleId == saleId)
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<ElectronicInvoice>> GetPendingForEmissionAsync(
        Guid companyId,
        DateTime now,
        DateTime cutoff,
        int limit,
        CancellationToken ct = default)
        => await Set()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(e => e.CompanyId == companyId
                && e.Status == EcfStatus.Signed
                && !e.RequiresReview
                && e.CreatedAt >= cutoff
                && e.CreatedAt <= now
                && (e.NextAttemptAt == null || e.NextAttemptAt <= now)
                && (e.LeaseUntil == null || e.LeaseUntil <= now))
            .OrderBy(e => e.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> GetCompanyIdsWithPendingEmissionAsync(
        DateTime now,
        DateTime cutoff,
        int limit,
        CancellationToken ct = default)
        => await Set()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(e => e.Status == EcfStatus.Signed
                && !e.RequiresReview
                && e.CreatedAt >= cutoff
                && e.CreatedAt <= now
                && (e.NextAttemptAt == null || e.NextAttemptAt <= now)
                && (e.LeaseUntil == null || e.LeaseUntil <= now))
            .Select(e => e.CompanyId)
            .Distinct()
            .Take(limit)
            .ToListAsync(ct);

    public async Task<bool> TryClaimAsync(
        Guid companyId,
        Guid invoiceId,
        string leaseOwner,
        DateTime leaseUntil,
        DateTime now,
        CancellationToken ct = default)
    {
        // The condition and the claim are one statement: two workers can see
        // the same document, but only one of them can take it.
        var claimed = await Set()
            .IgnoreQueryFilters()
            .Where(e => e.CompanyId == companyId
                && e.Id == invoiceId
                && e.Status == EcfStatus.Signed
                && !e.RequiresReview
                && (e.NextAttemptAt == null || e.NextAttemptAt <= now)
                && (e.LeaseUntil == null || e.LeaseUntil <= now))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(e => e.LeaseOwner, leaseOwner)
                .SetProperty(e => e.LeaseUntil, leaseUntil)
                .SetProperty(e => e.AttemptCount, e => e.AttemptCount + 1)
                .SetProperty(e => e.UpdatedAt, now), ct);

        return claimed == 1;
    }

    public async Task<ElectronicInvoice?> GetByCompanyAndIdAsync(
        Guid companyId,
        Guid invoiceId,
        CancellationToken ct = default)
        => await Set()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.CompanyId == companyId && e.Id == invoiceId, ct);

    public async Task<ElectronicInvoice?> GetByEncfAsync(Guid companyId, string issuerTaxId, string encf, CancellationToken ct = default)
        => await Set()
            .AsNoTracking()
            .Where(e => e.CompanyId == companyId && e.IssuerTaxId == issuerTaxId && e.ENCF == encf)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<ElectronicInvoice>> GetDueForCheckAsync(Guid companyId, DateTime now, CancellationToken ct = default)
        => await Set()
            .AsNoTracking()
            .Where(e => e.CompanyId == companyId
                && e.Status == EcfStatus.InProcess
                && e.NextCheckAt != null
                && e.NextCheckAt <= now)
            .OrderBy(e => e.NextCheckAt)
            .ToListAsync(ct);
}
