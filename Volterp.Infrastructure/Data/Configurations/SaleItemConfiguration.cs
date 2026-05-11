using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volterp.Domain.Entities;

namespace Volterp.Infrastructure.Data.Configurations;

public class SaleItemConfiguration : IEntityTypeConfiguration<SaleItem>
{
    public void Configure(EntityTypeBuilder<SaleItem> entity)
    {
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => e.SaleId);
        entity.HasIndex(e => e.ProductId);

        entity.Property(e => e.ProductName).IsRequired().HasMaxLength(200);
        entity.Property(e => e.ProductCategory).HasMaxLength(100);
        entity.Property(e => e.ProductCode).HasMaxLength(50);
        entity.Property(e => e.ProductImageUrl).HasMaxLength(500);
        entity.Property(e => e.UnitPrice).HasPrecision(18, 2);
        entity.Property(e => e.Subtotal).HasPrecision(18, 2);

        entity.HasOne(e => e.Sale)
            .WithMany(s => s.Items)
            .HasForeignKey(e => e.SaleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}