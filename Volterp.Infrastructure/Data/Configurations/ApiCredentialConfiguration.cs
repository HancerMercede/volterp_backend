using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volterp.Domain.Entities;

namespace Volterp.Infrastructure.Data.Configurations;

public class ApiCredentialConfiguration : IEntityTypeConfiguration<ApiCredential>
{
    public void Configure(EntityTypeBuilder<ApiCredential> entity)
    {
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => e.CompanyId);
        entity.ToTable("ApiCredentials");

        // The selector is the way in: two credentials of two tenants cannot
        // answer to the same one.
        entity.HasIndex(e => e.KeyId).IsUnique();

        entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
        entity.Property(e => e.KeyId).IsRequired().HasMaxLength(12);
        entity.Property(e => e.SecretHash).IsRequired().HasMaxLength(64);
        entity.Property(e => e.Status).HasConversion<int>().IsRequired();
    }
}
