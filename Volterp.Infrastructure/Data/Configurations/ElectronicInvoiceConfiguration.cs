using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volterp.Domain.Entities;

namespace Volterp.Infrastructure.Data.Configurations;

public class ElectronicInvoiceConfiguration : IEntityTypeConfiguration<ElectronicInvoice>
{
    public void Configure(EntityTypeBuilder<ElectronicInvoice> entity)
    {
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => e.CompanyId);

        // One document per sale: two emissions of the same sale would burn two
        // fiscal numbers and leave the client with two documents.
        entity.HasIndex(e => e.SaleId).IsUnique();

        // The e-NCF is unique per issuer: the same number can never exist twice
        // for the same RNC, no matter how many branches the tenant has.
        entity.HasIndex(e => new { e.IssuerTaxId, e.ENCF }).IsUnique();

        // The documents whose verdict check is due are looked up by company
        // and state.
        entity.HasIndex(e => new { e.CompanyId, e.Status, e.NextCheckAt });

        // The worker finds the documents whose attempt is due and whose lease
        // has expired.
        entity.HasIndex(e => new { e.CompanyId, e.Status, e.NextAttemptAt, e.LeaseUntil });

        entity.Property(e => e.ENCF).IsRequired().HasMaxLength(13);
        entity.Property(e => e.DocumentType).HasConversion<int>().IsRequired();
        entity.Property(e => e.IssuerTaxId).IsRequired().HasMaxLength(13);
        entity.Property(e => e.ReceiverTaxId).HasMaxLength(13);
        entity.Property(e => e.Subtotal).HasPrecision(18, 2);
        entity.Property(e => e.Itbis).HasPrecision(18, 2);
        entity.Property(e => e.Total).HasPrecision(18, 2);
        entity.Property(e => e.OriginalXml).HasColumnType("text");
        entity.Property(e => e.SignedXml).IsRequired().HasColumnType("text");
        entity.Property(e => e.TrackId).HasMaxLength(100);
        entity.Property(e => e.SecurityCode).HasMaxLength(20);
        entity.Property(e => e.QrUrl).HasMaxLength(500);
        entity.Property(e => e.Status).HasConversion<int>().IsRequired();
        entity.Property(e => e.VerdictNote).HasMaxLength(1000);
        entity.Property(e => e.LeaseOwner).HasMaxLength(200);

        entity.HasOne(e => e.Company)
            .WithMany(c => c.ElectronicInvoices)
            .HasForeignKey(e => e.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.Sale)
            .WithMany(s => s.ElectronicInvoices)
            .HasForeignKey(e => e.SaleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
