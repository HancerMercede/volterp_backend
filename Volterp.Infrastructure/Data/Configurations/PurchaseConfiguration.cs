using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volterp.Domain.Entities;

namespace Volterp.Infrastructure.Data.Configurations;

public class PurchaseConfiguration : IEntityTypeConfiguration<Purchase>
{
    public void Configure(EntityTypeBuilder<Purchase> entity)
    {
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => e.CompanyId);
        entity.HasIndex(e => e.SupplierId);
        entity.HasIndex(e => e.Status);

        entity.Property(e => e.SupplierName).HasMaxLength(200);
        entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(50).IsRequired();
        entity.Property(e => e.Total).HasPrecision(18, 2);
        entity.Property(e => e.Notes).HasMaxLength(1000);

        entity.HasOne(e => e.Company)
            .WithMany(c => c.Purchases)
            .HasForeignKey(e => e.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.Supplier)
            .WithMany()
            .HasForeignKey(e => e.SupplierId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}