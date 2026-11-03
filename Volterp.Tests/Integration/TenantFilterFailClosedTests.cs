using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Volterp.Application.Abstractions;
using Volterp.Domain.Entities;
using Volterp.Infrastructure.Data;
using Xunit;

namespace Volterp.Tests.Integration;

public class TenantFilterFailClosedTests
{
    private static readonly Guid CompanyId = Guid.Parse("0192b9c7-b6a3-7def-8c5e-1f2a3b4c5d01");
    private static readonly Guid SupplierId = Guid.Parse("0192b9c7-b6a3-7def-8c5e-1f2a3b4c5d02");

    private sealed class FixedTenantContext(Guid? companyId, bool isUnrestricted) : ITenantContext
    {
        public Guid? CompanyId => companyId;

        public bool IsUnrestricted => isUnrestricted;
    }

    private static SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        return connection;
    }

    private static async Task SeedAsync(VolterpDbContext db)
    {
        db.Companies.Add(new Company
        {
            Id = CompanyId,
            Name = "Company A",
            TaxId = "TA000000001",
            Address = "1 A Street",
            LegalName = "Company A Legal",
            Email = "a@company.test",
            Phone = "555-0001",
            IsActive = true
        });

        db.Suppliers.Add(new Supplier
        {
            Id = SupplierId,
            CompanyId = CompanyId,
            Name = "Supplier A",
            Email = "a@supplier.test",
            Phone = "555-0001",
            Address = "1 A Street",
            Category = "Wholesale",
            ContactPerson = "Alice",
            IsActive = true
        });

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task ResolvedTenantWithoutCompany_ReturnsNoRows()
    {
        using var connection = OpenConnection();
        var options = new DbContextOptionsBuilder<VolterpDbContext>()
            .UseSqlite(connection)
            .Options;
        using var db = new VolterpDbContext(options, new FixedTenantContext(null, isUnrestricted: false));
        db.Database.EnsureCreated();

        await SeedAsync(db);

        var visible = await db.Suppliers.ToListAsync();

        visible.Should().BeEmpty(
            "a resolved tenant context without a companyId must fail closed instead of exposing every tenant's rows.");
    }

    [Fact]
    public async Task UnrestrictedContext_StillReturnsEveryRow()
    {
        using var connection = OpenConnection();
        var options = new DbContextOptionsBuilder<VolterpDbContext>()
            .UseSqlite(connection)
            .Options;
        using var db = new VolterpDbContext(options, new NullTenantContext());
        db.Database.EnsureCreated();

        await SeedAsync(db);

        var visible = await db.Suppliers.ToListAsync();

        visible.Should().HaveCount(1,
            "design-time, migrations and seeders rely on unrestricted access.");
    }
}
