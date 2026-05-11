using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volterp.Domain.Entities;

namespace Volterp.Infrastructure.Data.Configurations;

public class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> entity)
    {
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => e.CompanyId);
        entity.HasIndex(e => e.Status);
        entity.HasIndex(e => e.ClienteId);

        entity.Property(e => e.ClienteName).HasMaxLength(200);
        entity.Property(e => e.Total).HasPrecision(18, 2);
        entity.Property(e => e.Notes).HasColumnType("text");
        entity.Property(e => e.Status).HasConversion<int>();

        entity.HasOne(e => e.Company)
            .WithMany(c => c.Sales)
            .HasForeignKey(e => e.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}