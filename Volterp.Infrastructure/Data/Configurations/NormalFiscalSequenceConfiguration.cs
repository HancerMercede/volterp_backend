using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volterp.Domain.Entities;

namespace Volterp.Infrastructure.Data.Configurations;

public class NormalFiscalSequenceConfiguration : IEntityTypeConfiguration<NormalFiscalSequence>
{
    public void Configure(EntityTypeBuilder<NormalFiscalSequence> entity)
    {
        entity.HasKey(e => e.Id);

        // One range per issuer, type and start: two ranges of the same issuer
        // cannot share their numbers.
        entity.HasIndex(e => new { e.IssuerTaxId, e.DocumentType, e.From }).IsUnique();
        entity.Property(e => e.DocumentType).HasConversion<int>().IsRequired();
        entity.Property(e => e.IssuerTaxId).IsRequired().HasMaxLength(20);
        entity.Property(e => e.From).IsRequired();
        entity.Property(e => e.To).IsRequired();
        entity.Property(e => e.CurrentSequence).IsRequired();

        entity.HasOne(e => e.Company)
            .WithMany()
            .HasForeignKey(e => e.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
