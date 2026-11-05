using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volterp.Domain.Entities;

namespace Volterp.Infrastructure.Data.Configurations;

public class ClientConfiguration:IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("Clients");
        
        builder.HasIndex(c => c.Email).IsUnique();
        builder.HasIndex(c => new { c.CompanyId, c.Code }).IsUnique();
        builder.HasIndex(c => new { c.CompanyId, c.BusinessDate });
        builder.Property(c => c.Code).HasMaxLength(50);
        builder.Property(c => c.Name).IsRequired().HasMaxLength(100);
        builder.Property(c => c.Email).IsRequired().HasMaxLength(100);
        builder.Property(c => c.Phone).IsRequired().HasMaxLength(70);
        builder.Property(c => c.Address).IsRequired().HasMaxLength(200);
        builder.Property(c => c.TaxId).HasMaxLength(20);
        builder.Property(c => c.CompanyId).IsRequired();
    }
}