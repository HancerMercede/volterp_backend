using Microsoft.EntityFrameworkCore;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.Infrastructure.Data;

namespace Volterp.Infrastructure.Repositories;

public class SaleRepository(VolterpDbContext context) : RepositoryBase<Sale>(context), ISaleRepository
{
    public async Task<Sale?> GetSaleByIdAsync(Guid id, CancellationToken ct = default)
        => await GetByCondictionsAsync(s => s.Id == id,ct,
            s => s
                .Include(i => i.Items)
                .Include(i => i.Company)
                .Include(i => i.ElectronicInvoices));


    public async Task<PagedResult<Sale>> GetAllSalesAsync(int pageNumber, int pageSize,
        CancellationToken ct = default)
    => await GetAllAsync(_ => true, pageNumber, pageSize,ct,
        s=> s.Include(i=>i.Items));


    public async Task<PagedResult<Sale>> GetSalesByStatusAsync(SaleStatus status,
        int pageNumber, int pageSize, CancellationToken ct = default)
    {
        return await GetAllAsync(s => s.Status == status, pageNumber, pageSize, ct);
    }

    public async Task<Sale> AddSaleAsync(Sale sale, CancellationToken ct = default)
    {
        await AddAsync(sale, ct);
        return sale;
    }

    public async Task UpdateSaleAsync(Sale sale, CancellationToken ct = default)
        => await UpdateAsync(sale, ct);

    public async Task DeleteSaleAsync(Guid id, CancellationToken ct = default)
    {
        var sale = await GetByIdAsync(id, ct);
        if (sale is not null)
            await DeleteAsync(sale, ct);
    }

    public async Task<bool> ExistsCodeAsync(Guid companyId, string code, Guid? excludeId = null, CancellationToken ct = default)
    {
        if (excludeId.HasValue)
            return await ExistsAsync(s => s.CompanyId == companyId && s.Code == code && s.Id != excludeId.Value, ct);

        return await ExistsAsync(s => s.CompanyId == companyId && s.Code == code, ct);
    }

    // The sweep runs without an HTTP request: the filter of the tenant
    // would hide the sales of every company, so the query goes without it.
    // The sale carries its company.
    public async Task<IReadOnlyList<Sale>> GetPendingOlderThanAsync(DateTime cutoff, CancellationToken ct = default)
    {
        var sales = await Set()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(sale => sale.Status == SaleStatus.Pending && sale.CreatedAt <= cutoff)
            .ToListAsync(ct);

        if (sales.Count == 0)
            return sales;

        // The items are read apart: the filter of the tenant would hide
        // them too, and the stock they reserved has to come back.
        var saleIds = sales.Select(sale => sale.Id).ToList();

        var items = await context.Set<SaleItem>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(item => saleIds.Contains(item.SaleId))
            .ToListAsync(ct);

        foreach (var sale in sales)
            sale.Items = items.Where(item => item.SaleId == sale.Id).ToList();

        return sales;
    }

    // The products that the pending sales reserved. The sweep runs without
    // an HTTP request too: the filter of the tenant would hide them, and
    // the stock they hold would never come back.
    public async Task<IReadOnlyList<Product>> GetReservedProductsAsync(IEnumerable<Guid> productIds, CancellationToken ct = default)
        => await context.Set<Product>()
            .IgnoreQueryFilters()
            .Where(product => productIds.Contains(product.Id))
            .ToListAsync(ct);
}
