using Microsoft.EntityFrameworkCore;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
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

    public async Task<ElectronicInvoice?> GetByEncfAsync(Guid companyId, string issuerTaxId, string encf, CancellationToken ct = default)
        => await Set()
            .AsNoTracking()
            .Where(e => e.CompanyId == companyId && e.IssuerTaxId == issuerTaxId && e.ENCF == encf)
            .FirstOrDefaultAsync(ct);
}
