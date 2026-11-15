using Microsoft.EntityFrameworkCore;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Infrastructure.Data;

namespace Volterp.Infrastructure.Repositories;

public class ReceivedElectronicInvoiceRepository(VolterpDbContext context)
    : RepositoryBase<ReceivedElectronicInvoice>(context), IReceivedElectronicInvoiceRepository
{
    public async Task<ReceivedElectronicInvoice?> GetByIssuerAndEncfAsync(
        string issuerTaxId,
        string encf,
        CancellationToken ct = default)
        => await Set()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                invoice => invoice.IssuerTaxId == issuerTaxId && invoice.ENCF == encf,
                ct);

    public async Task<ReceivedElectronicInvoice?> GetByCompanyAndIdAsync(
        Guid companyId,
        Guid id,
        CancellationToken ct = default)
        => await Set()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                invoice => invoice.CompanyId == companyId && invoice.Id == id,
                ct);
}
