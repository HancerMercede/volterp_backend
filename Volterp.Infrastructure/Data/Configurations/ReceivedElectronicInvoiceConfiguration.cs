using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volterp.Domain.Entities;

namespace Volterp.Infrastructure.Data.Configurations;

public class ReceivedElectronicInvoiceConfiguration : IEntityTypeConfiguration<ReceivedElectronicInvoice>
{
    public void Configure(EntityTypeBuilder<ReceivedElectronicInvoice> entity)
    {
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => e.CompanyId);

        // The issuer and the e-NCF are the natural identity of the document.
        entity.HasIndex(e => new { e.IssuerTaxId, e.ENCF }).IsUnique();

        entity.Property(e => e.IssuerTaxId).IsRequired().HasMaxLength(20);
        entity.Property(e => e.ReceiverTaxId).IsRequired().HasMaxLength(20);
        entity.Property(e => e.ENCF).IsRequired().HasMaxLength(13);
        entity.Property(e => e.DocumentType).HasConversion<int>().IsRequired();
        entity.Property(e => e.SignedXml).IsRequired().HasColumnType("text");
        entity.Property(e => e.ApprovalStatus).HasConversion<int>().IsRequired();
        entity.Property(e => e.ApprovalDecidedBy).HasMaxLength(100);
        entity.Property(e => e.ApprovalReason).HasMaxLength(1000);

        entity.HasOne(e => e.Company)
            .WithMany()
            .HasForeignKey(e => e.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
