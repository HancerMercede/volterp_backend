using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Volterp.Application.Abstractions;
using Volterp.Domain.Abstractions;
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
    public DbSet<ElectronicInvoiceSequence> ElectronicInvoiceSequences => Set<ElectronicInvoiceSequence>();
    public DbSet<CompanyCertificate> CompanyCertificates => Set<CompanyCertificate>();
    public DbSet<ElectronicInvoice> ElectronicInvoices => Set<ElectronicInvoice>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VolterpDbContext).Assembly);
        ApplyTenantFilters(modelBuilder);
    }

    private void ApplyTenantFilters(ModelBuilder modelBuilder)
    {
        var entityTypes = modelBuilder.Model.GetEntityTypes().ToList();

        TenantScopeGuard.EnsureDeclared(entityTypes);

        foreach (var entityType in entityTypes)
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
            else if (typeof(ITenantScoped).IsAssignableFrom(clrType))
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

    private void ApplyDirectFilterGeneric<TEntity>(ModelBuilder modelBuilder) where TEntity : class, ITenantScoped
    {
        modelBuilder.Entity<TEntity>()
            .HasQueryFilter(e => _tenantContext.IsUnrestricted || e.CompanyId == _tenantContext.CompanyId);
    }
}