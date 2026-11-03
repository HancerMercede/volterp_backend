using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volterp.Domain.Entities;

namespace Volterp.Infrastructure.Data.Configurations;

public class CompanyCertificateConfiguration : IEntityTypeConfiguration<CompanyCertificate>
{
    public void Configure(EntityTypeBuilder<CompanyCertificate> entity)
    {
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => e.CompanyId);
        entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
        entity.Property(e => e.TaxId).IsRequired().HasMaxLength(20);
        entity.Property(e => e.EncryptedCredential).IsRequired();
        entity.Property(e => e.Status).HasConversion<int>().IsRequired();

        entity.HasOne(e => e.Company)
            .WithMany(c => c.Certificates)
            .HasForeignKey(e => e.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
