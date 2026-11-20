using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volterp.Domain.Entities;

namespace Volterp.Infrastructure.Data.Configurations;

public class UserRoleAssignmentConfiguration : IEntityTypeConfiguration<UserRoleAssignment>
{
    public void Configure(EntityTypeBuilder<UserRoleAssignment> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Role).IsRequired();
        entity.HasIndex(e => e.CompanyId);

        // A user holds a role once: the same role cannot be assigned twice.
        entity.HasIndex(e => new { e.UserId, e.Role }).IsUnique();

        entity.HasOne(e => e.User)
              .WithMany(u => u.Roles)
              .HasForeignKey(e => e.UserId)
              .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.Company)
              .WithMany()
              .HasForeignKey(e => e.CompanyId)
              .OnDelete(DeleteBehavior.Restrict);
    }
}
