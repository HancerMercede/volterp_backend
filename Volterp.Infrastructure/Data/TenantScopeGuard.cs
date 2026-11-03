using Microsoft.EntityFrameworkCore.Metadata;
using Volterp.Domain.Abstractions;

namespace Volterp.Infrastructure.Data;

public static class TenantScopeGuard
{
    public static void EnsureDeclared(IEnumerable<IReadOnlyEntityType> entityTypes)
    {
        var undeclared = entityTypes
            .Where(t => t.ClrType.GetProperty(nameof(ITenantScoped.CompanyId)) is not null)
            .Where(t => !typeof(ITenantScoped).IsAssignableFrom(t.ClrType))
            .Select(t => t.ClrType.Name)
            .ToList();

        if (undeclared.Count == 0)
            return;

        throw new InvalidOperationException(
            $"Entities carrying a {nameof(ITenantScoped.CompanyId)} must implement {nameof(ITenantScoped)} so the tenant filter applies: {string.Join(", ", undeclared)}.");
    }
}
