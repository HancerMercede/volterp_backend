using Microsoft.EntityFrameworkCore;
using Volterp.Domain.Entities;

namespace Volterp.Infrastructure.Data;

public class VolterpDbContext(DbContextOptions<VolterpDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VolterpDbContext).Assembly);
    }
}