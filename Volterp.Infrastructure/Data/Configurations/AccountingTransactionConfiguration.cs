using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;

namespace Volterp.Infrastructure.Data.Configurations;

public class AccountingTransactionConfiguration : IEntityTypeConfiguration<AccountingTransaction>
{
    public void Configure(EntityTypeBuilder<AccountingTransaction> entity)
    {
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => e.CompanyId);
        entity.HasIndex(e => e.TransactionDate);
        entity.HasIndex(e => e.ReferenceNumber);
        entity.HasIndex(e => e.Category);
        entity.HasIndex(e => e.Status);

        entity.Property(e => e.TransactionType).HasConversion<string>().HasMaxLength(50).IsRequired();
        entity.Property(e => e.Amount).HasPrecision(18, 2).IsRequired();
        entity.Property(e => e.Description).HasMaxLength(500).IsRequired();
        entity.Property(e => e.ReferenceNumber).HasMaxLength(100).IsRequired();
        entity.Property(e => e.TransactionDate).IsRequired();
        entity.Property(e => e.Category).HasMaxLength(100).IsRequired();
        entity.Property(e => e.Notes).HasMaxLength(1000);
        entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(50).IsRequired();

        entity.HasOne(e => e.Company)
            .WithMany(c => c.AccountingTransactions)
            .HasForeignKey(e => e.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}