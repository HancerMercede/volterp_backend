using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volterp.Domain.Entities;

namespace Volterp.Infrastructure.Data.Configurations;

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> entity)
    {
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => e.CompanyId);
        entity.HasIndex(e => e.Name);
        entity.HasIndex(e => e.Category);
        entity.HasIndex(e => e.IsActive);

        entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
        entity.Property(e => e.Email).HasMaxLength(200);
        entity.Property(e => e.Phone).HasMaxLength(50);
        entity.Property(e => e.Address).HasMaxLength(500);
        entity.Property(e => e.Category).HasMaxLength(100);
        entity.Property(e => e.ContactPerson).HasMaxLength(200);

        entity.HasOne(e => e.Company)
            .WithMany(c => c.Suppliers)
            .HasForeignKey(e => e.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}