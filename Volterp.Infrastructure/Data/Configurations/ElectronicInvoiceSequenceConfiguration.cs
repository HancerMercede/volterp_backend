using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volterp.Domain.Entities;

namespace Volterp.Infrastructure.Data.Configurations;

public class ElectronicInvoiceSequenceConfiguration : IEntityTypeConfiguration<ElectronicInvoiceSequence>
{
    public void Configure(EntityTypeBuilder<ElectronicInvoiceSequence> entity)
    {
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => new { e.CompanyId, e.DocumentType }).IsUnique();
        entity.Property(e => e.DocumentType).HasConversion<int>().IsRequired();
        entity.Property(e => e.CurrentSequence).IsRequired();

        entity.HasOne(e => e.Company)
            .WithMany(c => c.ElectronicInvoiceSequences)
            .HasForeignKey(e => e.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
