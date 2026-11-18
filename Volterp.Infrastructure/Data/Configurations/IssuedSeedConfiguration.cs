using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volterp.Domain.Entities;

namespace Volterp.Infrastructure.Data.Configurations;

public class IssuedSeedConfiguration : IEntityTypeConfiguration<IssuedSeed>
{
    public void Configure(EntityTypeBuilder<IssuedSeed> entity)
    {
        entity.HasKey(e => e.Id);
        entity.ToTable("IssuedSeeds");

        entity.Property(e => e.Value).IsRequired().HasMaxLength(64);

        // The value is the way in: a seed cannot be issued twice with the
        // same value, or a third party could replay one of them.
        entity.HasIndex(e => e.Value).IsUnique();
    }
}
