using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volterp.Domain.Entities;

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

        entity.HasData(new Company
        {
            Id = 1,
            Name = "HM Software Solutions",
            TaxId = "HM123456789",
            IsActive = true,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
    }
}