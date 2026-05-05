using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volterp.Domain.Entities;

namespace Volterp.Infrastructure.Data.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> entity)
    {
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => e.CompanyId);
        entity.HasIndex(e => e.CategoryId);

        entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
        entity.Property(e => e.Category).IsRequired().HasMaxLength(100);
        entity.Property(e => e.Description).HasMaxLength(1000);
        entity.Property(e => e.ImageUrl).HasColumnType("text");
        entity.Property(e => e.Price).HasPrecision(18, 2);
        entity.Property(e => e.Stock).HasDefaultValue(0);
        entity.Property(e => e.IsActive).HasDefaultValue(true);

        entity.HasOne(e => e.Company)
            .WithMany(c => c.Products)
            .HasForeignKey(e => e.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.CategoryEntity)
            .WithMany(c => c.Products)
            .HasForeignKey(e => e.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}