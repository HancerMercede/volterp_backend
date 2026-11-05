using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;

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

        entity.HasData(new User
        {
            Id = new Guid("00000002-0000-0000-0000-000000000001"),
            // The seeded admin is the first user of its company, so the backfill
            // of the AddEntityCodes migration numbers it "Usu-001". Declaring the
            // same code here keeps the seed and the backfill in agreement: EF
            // re-emits this row as an UpdateData, and an empty code would wipe
            // the one the migration assigned.
            Code = "Usu-001",
            Username = "admin",
            PasswordHash = "$2a$12$XLgan9fXGIGfO.UcBuUweegkVWrYJqDgOZY48h4/udZJyNASNol3O",
            Email = "admin@hm.com",
            FullName = "Administrador",
            Role = UserRole.SuperAdmin,
            CompanyId = new Guid("00000001-0000-0000-0000-000000000001"),
            IsActive = true,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
    }
}
