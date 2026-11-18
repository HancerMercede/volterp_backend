using Microsoft.EntityFrameworkCore;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.Infrastructure.Data;

namespace Volterp.Infrastructure.Repositories;

public class CompanyIssuerRepository(VolterpDbContext context)
    : RepositoryBase<CompanyIssuer>(context), ICompanyIssuerRepository
{
    public async Task<IReadOnlyList<string>> IssuerTaxIdsAsync(Guid companyId, CancellationToken ct = default)
    {
        var company = await context.Set<Company>()
            .AsNoTracking()
            .Where(candidate => candidate.Id == companyId)
            .Select(candidate => candidate.TaxId)
            .SingleOrDefaultAsync(ct);

        if (company is null)
            return [];

        var branches = await context.Set<CompanyIssuer>()
            .AsNoTracking()
            .Where(issuer => issuer.CompanyId == companyId && issuer.Status == EntityStatus.Active)
            .Select(issuer => issuer.TaxId)
            .ToListAsync(ct);

        return [company, ..branches];
    }

    public async Task<string> ResolveIssuerTaxIdAsync(
        Guid companyId, string? requestedTaxId, CancellationToken ct = default)
    {
        var issuers = await IssuerTaxIdsAsync(companyId, ct);

        if (string.IsNullOrWhiteSpace(requestedTaxId))
        {
            if (issuers.Count == 0)
                throw new InvalidOperationException($"The company {companyId} does not exist.");

            return issuers[0];
        }

        var issuer = issuers.FirstOrDefault(candidate => candidate == requestedTaxId.Trim());

        if (issuer is null)
            throw new InvalidOperationException(
                $"The issuer RNC {requestedTaxId} is not one of the issuers of this company.");

        return issuer;
    }

    public async Task<Company?> CompanyOfIssuerAsync(string taxId, CancellationToken ct = default)
    {
        var owners = new List<Guid>();

        owners.AddRange(await context.Set<Company>()
            .AsNoTracking()
            .Where(company => company.TaxId == taxId)
            .Select(company => company.Id)
            .ToListAsync(ct));

        owners.AddRange(await context.Set<CompanyIssuer>()
            .AsNoTracking()
            .Where(issuer => issuer.TaxId == taxId)
            .Select(issuer => issuer.CompanyId)
            .Distinct()
            .ToListAsync(ct));

        var distinct = owners.Distinct().ToList();

        if (distinct.Count > 1)
            throw new InvalidOperationException("The receiver RNC is ambiguous.");

        if (distinct.Count == 0)
            return null;

        return await context.Set<Company>()
            .AsNoTracking()
            .FirstOrDefaultAsync(company => company.Id == distinct[0], ct);
    }
}
