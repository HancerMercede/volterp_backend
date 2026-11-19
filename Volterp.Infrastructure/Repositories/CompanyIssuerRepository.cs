using Microsoft.EntityFrameworkCore;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.Domain.ValueObjects;
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
            .IgnoreQueryFilters()
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

        // The RNC travels normalized: "1-01-00000-1" and "101000001" are the
        // same taxpayer.
        var normalized = Normalize(requestedTaxId);

        var issuer = issuers.FirstOrDefault(candidate => candidate == normalized);

        if (issuer is null)
            throw new InvalidOperationException(
                $"The issuer RNC {requestedTaxId} is not one of the issuers of this company.");

        return issuer;
    }

    public async Task<Company?> CompanyOfIssuerAsync(string taxId, CancellationToken ct = default)
    {
        var owners = await OwnersOfAsync(taxId, ct);

        if (owners.Count > 1)
            throw new InvalidOperationException("The receiver RNC is ambiguous.");

        if (owners.Count == 0)
            return null;

        return await context.Set<Company>()
            .AsNoTracking()
            .FirstOrDefaultAsync(company => company.Id == owners[0], ct);
    }

    public async Task<IReadOnlyList<Guid>> OwnersOfAsync(string taxId, CancellationToken ct = default)
    {
        var normalized = Normalize(taxId);

        var owners = new List<Guid>();

        owners.AddRange(await context.Set<Company>()
            .AsNoTracking()
            .Where(company => company.TaxId == normalized)
            .Select(company => company.Id)
            .ToListAsync(ct));

        // The reception arrives with the certificate of the third party and
        // without a tenant claim, so the filter of the tenant would leave
        // zero rows: the lookup goes without it and asks the company
        // explicitly.
        owners.AddRange(await context.Set<CompanyIssuer>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(issuer => issuer.TaxId == normalized)
            .Select(issuer => issuer.CompanyId)
            .ToListAsync(ct));

        return owners.Distinct().ToList();
    }

    // The RNC travels normalized everywhere: the certificate and the
    // company guard it, the ranges do not.
    private static string Normalize(string taxId)
        => Rnc.TryParse(taxId, out var rnc) ? rnc.Value : taxId.Trim();
}
