using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volterp.Domain.Entities;

namespace Volterp.Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> entity)
    {
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => e.Username).IsUnique();
        entity.HasIndex(e => e.Email).IsUnique();
        entity.HasIndex(e => new { e.CompanyId, e.Code }).IsUnique();
        entity.Property(e => e.Code).HasMaxLength(50);
        entity.Property(e => e.Username).IsRequired().HasMaxLength(50);
        entity.Property(e => e.Email).IsRequired().HasMaxLength(100);
        entity.Property(e => e.PasswordHash).IsRequired();
        entity.Property(e => e.Role).IsRequired().HasMaxLength(30);
        entity.Property(e => e.FullName).IsRequired().HasMaxLength(100);

        entity.HasOne(e => e.Company)
              .WithMany(c => c.Users)
              .HasForeignKey(e => e.CompanyId)
              .OnDelete(DeleteBehavior.Restrict);
    }
}
