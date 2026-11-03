using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Volterp.Application.Abstractions;
using Volterp.Domain.Entities;

namespace Volterp.Infrastructure.Data;

public class VolterpDbContext(
    DbContextOptions<VolterpDbContext> options,
    ITenantContext? tenantContext = null) : DbContext(options)
{
    private readonly ITenantContext _tenantContext = tenantContext ?? new NullTenantContext();

    public DbSet<User> Users => Set<User>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<PurchaseItem> PurchaseItems => Set<PurchaseItem>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<AccountingTransaction> AccountingTransactions => Set<AccountingTransaction>();
    public DbSet<Client> Clients => Set<Client>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VolterpDbContext).Assembly);
        ApplyTenantFilters(modelBuilder);
    }

    private void ApplyTenantFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;

            if (clrType == typeof(SaleItem))
            {
                modelBuilder.Entity<SaleItem>()
                    .HasQueryFilter(e => _tenantContext.IsUnrestricted || e.Sale.CompanyId == _tenantContext.CompanyId);
            }
            else if (clrType == typeof(PurchaseItem))
            {
                modelBuilder.Entity<PurchaseItem>()
                    .HasQueryFilter(e => _tenantContext.IsUnrestricted || e.Purchase.CompanyId == _tenantContext.CompanyId);
            }
            else if (clrType == typeof(Company))
            {
                continue;
            }
            else if (clrType.GetProperty("CompanyId") is not null)
            {
                ApplyDirectFilter(modelBuilder, clrType);
            }
        }
    }

    private void ApplyDirectFilter(ModelBuilder modelBuilder, Type clrType)
    {
        var method = typeof(VolterpDbContext)
            .GetMethod(nameof(ApplyDirectFilterGeneric), BindingFlags.Instance | BindingFlags.NonPublic)!
            .MakeGenericMethod(clrType);
        method.Invoke(this, [modelBuilder]);
    }

    private void ApplyDirectFilterGeneric<TEntity>(ModelBuilder modelBuilder) where TEntity : class
    {
        modelBuilder.Entity<TEntity>()
            .HasQueryFilter(e => _tenantContext.IsUnrestricted || EF.Property<Guid>(e, "CompanyId") == _tenantContext.CompanyId);
    }
}