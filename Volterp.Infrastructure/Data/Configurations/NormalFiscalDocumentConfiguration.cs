using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volterp.Domain.Entities;

namespace Volterp.Infrastructure.Data.Configurations;

public class NormalFiscalDocumentConfiguration : IEntityTypeConfiguration<NormalFiscalDocument>
{
    public void Configure(EntityTypeBuilder<NormalFiscalDocument> entity)
    {
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => e.CompanyId);

        // One document per sale: two documents of the same sale would burn two
        // fiscal numbers and leave the client with two documents.
        entity.HasIndex(e => e.SaleId).IsUnique();

        // The number is unique per issuer: the same number can never exist
        // twice for the same RNC, no matter how many branches the tenant has.
        entity.HasIndex(e => new { e.IssuerTaxId, e.Number }).IsUnique();

        entity.Property(e => e.Number).IsRequired().HasMaxLength(11);
        entity.Property(e => e.DocumentType).HasConversion<int>().IsRequired();
        entity.Property(e => e.IssuerTaxId).IsRequired().HasMaxLength(13);
        entity.Property(e => e.ReceiverTaxId).HasMaxLength(13);
        entity.Property(e => e.ReceiverName).HasMaxLength(200);
        entity.Property(e => e.Subtotal).HasPrecision(18, 2);
        entity.Property(e => e.Itbis).HasPrecision(18, 2);
        entity.Property(e => e.Total).HasPrecision(18, 2);
        entity.Property(e => e.IssuedAt).IsRequired();

        entity.HasOne<Company>()
            .WithMany()
            .HasForeignKey(e => e.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne<Sale>()
            .WithMany()
            .HasForeignKey(e => e.SaleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
