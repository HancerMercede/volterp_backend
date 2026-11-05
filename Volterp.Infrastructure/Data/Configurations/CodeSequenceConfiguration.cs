using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volterp.Domain.Entities;

namespace Volterp.Infrastructure.Data.Configurations;

public class CodeSequenceConfiguration : IEntityTypeConfiguration<CodeSequence>
{
    public void Configure(EntityTypeBuilder<CodeSequence> entity)
    {
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => new { e.CompanyId, e.EntityType }).IsUnique();
        entity.Property(e => e.EntityType).HasConversion<int>().IsRequired();
        entity.Property(e => e.CurrentValue).IsRequired();

        entity.HasOne(e => e.Company)
            .WithMany(c => c.CodeSequences)
            .HasForeignKey(e => e.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
