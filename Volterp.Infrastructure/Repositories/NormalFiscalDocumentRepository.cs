using Microsoft.EntityFrameworkCore;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Infrastructure.Data;

namespace Volterp.Infrastructure.Repositories;

public class NormalFiscalDocumentRepository(VolterpDbContext context)
    : RepositoryBase<NormalFiscalDocument>(context), INormalFiscalDocumentRepository
{
    // The company travels by hand: the document of a sale is looked up for
    // the company that asks for it, whatever the tenant context is.
    public async Task<NormalFiscalDocument?> GetBySaleIdAsync(
        Guid companyId,
        Guid saleId,
        CancellationToken ct = default)
        => await Set()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.CompanyId == companyId && e.SaleId == saleId, ct);
}
