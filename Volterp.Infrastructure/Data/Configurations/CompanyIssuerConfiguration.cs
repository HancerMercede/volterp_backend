using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volterp.Domain.Entities;

namespace Volterp.Infrastructure.Data.Configurations;

public class CompanyIssuerConfiguration : IEntityTypeConfiguration<CompanyIssuer>
{
    public void Configure(EntityTypeBuilder<CompanyIssuer> entity)
    {
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => e.CompanyId);
        entity.HasIndex(e => new { e.CompanyId, e.TaxId }).IsUnique();
        entity.Property(e => e.TaxId).IsRequired().HasMaxLength(20);
        entity.Property(e => e.LegalName).IsRequired().HasMaxLength(200);
        entity.Property(e => e.Status).HasConversion<int>().IsRequired();

        entity.HasOne(e => e.Company)
            .WithMany(c => c.Issuers)
            .HasForeignKey(e => e.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
