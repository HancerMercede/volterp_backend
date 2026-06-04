using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;

namespace Volterp.Infrastructure.Data.Configurations;

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> entity)
    {
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => e.CompanyId);
        entity.HasIndex(e => e.Email);
        entity.HasIndex(e => e.Status);
        entity.HasIndex(e => e.Department);

        entity.Property(e => e.FirstName).HasMaxLength(200).IsRequired();
        entity.Property(e => e.LastName).HasMaxLength(200).IsRequired();
        entity.Property(e => e.Email).HasMaxLength(200);
        entity.Property(e => e.Phone).HasMaxLength(50);
        entity.Property(e => e.Position).HasMaxLength(100).IsRequired();
        entity.Property(e => e.Department).HasMaxLength(100);
        entity.Property(e => e.HireDate).IsRequired();
        entity.Property(e => e.Salary).HasPrecision(18, 2);
        entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(50).IsRequired();
        entity.Property(e => e.DirectManagerId);
        entity.Property(e => e.WorkSchedule).HasMaxLength(100);
        entity.Property(e => e.ImageUrl).HasColumnType("text");
        entity.Property(e => e.AFP).HasMaxLength(100);
        entity.Property(e => e.ARS).HasMaxLength(100);
        entity.Property(e => e.NSS).HasMaxLength(50);
        entity.Property(e => e.Bank).HasMaxLength(100);
        entity.Property(e => e.AccountNumber).HasMaxLength(50);

        entity.HasOne(e => e.Company)
            .WithMany(c => c.Employees)
            .HasForeignKey(e => e.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}