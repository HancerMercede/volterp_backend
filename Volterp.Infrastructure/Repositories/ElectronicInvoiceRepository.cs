using Microsoft.EntityFrameworkCore;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
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
                // The replacement of a paper document waits for the
                // contingency to be lifted, and the ceiling of the
                // contingency of the DGII does not apply to it.
                && (e.ReplacesContingencyDocument || e.CreatedAt >= cutoff)
                && e.CreatedAt <= now
                && (e.SendNotBefore == null
                    || e.SendNotBefore <= now
                    // The taxpayer lifted the contingency before the day the
                    // replacement was waiting for: it goes from that moment
                    // on, because that is when the contingency was lifted.
                    || (e.ReplacesContingencyDocument
                        && e.Company.ContingencyStatus == CompanyContingencyStatus.None)
                    // A replacement that waited longer than the ceiling is
                    // looked at even before its day: it may need a human.
                    || (e.ReplacesContingencyDocument && e.CreatedAt < cutoff))
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
            .Where(e => (e.Status == EcfStatus.Signed
                    && !e.RequiresReview
                    && (e.ReplacesContingencyDocument || e.CreatedAt >= cutoff)
                    && e.CreatedAt <= now
                    && (e.SendNotBefore == null
                        || e.SendNotBefore <= now
                        || (e.ReplacesContingencyDocument
                            && e.Company.ContingencyStatus == CompanyContingencyStatus.None)
                        || (e.ReplacesContingencyDocument && e.CreatedAt < cutoff))
                    && (e.NextAttemptAt == null || e.NextAttemptAt <= now)
                    && (e.LeaseUntil == null || e.LeaseUntil <= now))
                || ((e.Status == EcfStatus.Sent || e.Status == EcfStatus.InProcess)
                    && e.NextCheckAt != null
                    && e.NextCheckAt <= now))
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
        // The claim says who takes the document, not whether it may go: the
        // query of the pending documents decides that. A replacement whose
        // contingency was lifted before its day is already in that list, and
        // the claim may not keep it waiting.
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
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(e => e.CompanyId == companyId
                && (e.Status == EcfStatus.Sent || e.Status == EcfStatus.InProcess)
                && e.NextCheckAt != null
                && e.NextCheckAt <= now)
            .OrderBy(e => e.NextCheckAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ElectronicInvoice>> GetOpenAsync(Guid companyId, CancellationToken ct = default)
    {
        // The verdicts that close a document come from the library: the query
        // and the classification of a document cannot disagree about them.
        var closed = EcfFiscalStateClassifier.ClosedStatuses;

        // The signed XML is not read: the surface only needs the state of the
        // document, and the XML is the heaviest column of the table. The
        // document comes back with the columns the surface reads.
        return await Set()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(e => e.CompanyId == companyId && !closed.Contains(e.Status))
            .Select(e => new ElectronicInvoice
            {
                Id = e.Id,
                CompanyId = e.CompanyId,
                SaleId = e.SaleId,
                ENCF = e.ENCF,
                DocumentType = e.DocumentType,
                Status = e.Status,
                VerdictNote = e.VerdictNote,
                RequiresReview = e.RequiresReview,
                SentAt = e.SentAt,
                NextCheckAt = e.NextCheckAt,
                StatusCheckCount = e.StatusCheckCount,
                RawResponse = e.RawResponse,
                SequenceConsumed = e.SequenceConsumed,
                ReceivedAt = e.ReceivedAt,
                LastError = e.LastError,
                CreatedAt = e.CreatedAt
            })
            .ToListAsync(ct);
    }
}
