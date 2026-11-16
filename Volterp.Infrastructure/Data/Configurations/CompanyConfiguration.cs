using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;

namespace Volterp.Infrastructure.Data.Configurations;

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> entity)
    {
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => e.TaxId).IsUnique();
        entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
        entity.Property(e => e.TaxId).IsRequired().HasMaxLength(20);
        entity.Property(e => e.Address).IsRequired().HasMaxLength(100);
        entity.Property(e => e.LegalName).IsRequired().HasMaxLength(50);
        entity.Property(e=>e.Email).IsRequired().HasMaxLength(50);
        entity.Property(e => e.Phone).IsRequired().HasMaxLength(30);
        entity.Property(e => e.IsActive).HasDefaultValue(true);
        entity.Property(e => e.EmissionMode)
            .HasConversion<int>()
            .IsRequired()
            .HasSentinel((CompanyEmissionMode)0)
            .HasDefaultValue(CompanyEmissionMode.Electronic);
        entity.Property(e => e.ContingencyStatus)
            .HasConversion<int>()
            .IsRequired()
            .HasDefaultValue(CompanyContingencyStatus.None);
    }
}
