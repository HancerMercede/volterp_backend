using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volterp.Domain.Entities;

namespace Volterp.Infrastructure.Data.Configurations;

public class PurchaseItemConfiguration : IEntityTypeConfiguration<PurchaseItem>
{
    public void Configure(EntityTypeBuilder<PurchaseItem> entity)
    {
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => e.PurchaseId);
        entity.HasIndex(e => e.ProductId);

        entity.Property(e => e.ProductName).HasMaxLength(200).IsRequired();
        entity.Property(e => e.ProductCode).HasMaxLength(50);
        entity.Property(e => e.Quantity).IsRequired();
        entity.Property(e => e.UnitPrice).HasPrecision(18, 2);
        entity.Property(e => e.Subtotal).HasPrecision(18, 2);

        entity.HasOne(e => e.Purchase)
            .WithMany(p => p.Items)
            .HasForeignKey(e => e.PurchaseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}