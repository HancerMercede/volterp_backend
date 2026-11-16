using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.Infrastructure.Data;
using Xunit;

namespace Volterp.Tests.Infrastructure;

// The mode of emission has a default in the database: a company that does not
// set it is electronic. The sentinel tells the invalid value of the enum
// apart from the one that was never set, so an invalid value never reaches
// the database.
public class CompanyEmissionModeSentinelTests
{
    [Fact]
    public async Task ACompanyThatDoesNotSetTheModeIsElectronic()
    {
        await using var db = await DbAsync();

        db.Companies.Add(Company());
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var saved = await db.Companies.SingleAsync();
        saved.EmissionMode.Should().Be(CompanyEmissionMode.Electronic);
    }

    [Fact]
    public async Task ACompanyThatSetsTheNormalModeKeepsIt()
    {
        await using var db = await DbAsync();

        db.Companies.Add(Company(emissionMode: CompanyEmissionMode.Normal));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var saved = await db.Companies.SingleAsync();
        saved.EmissionMode.Should().Be(CompanyEmissionMode.Normal);
    }

    [Fact]
    public async Task ACompanyWithAnInvalidModeNeverPersistsIt()
    {
        await using var db = await DbAsync();

        db.Companies.Add(Company(emissionMode: (CompanyEmissionMode)0));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var saved = await db.Companies.SingleAsync();
        saved.EmissionMode.Should().Be(CompanyEmissionMode.Electronic,
            "the invalid value of the enum means that the mode was never set");
    }

    private static Company Company(CompanyEmissionMode? emissionMode = null)
        => new()
        {
            Id = Guid.CreateVersion7(),
            Name = "Company",
            TaxId = "101500018",
            Address = "1 A Street",
            LegalName = "Company Legal",
            Email = "company@company.test",
            Phone = "555-0001",
            IsActive = true,
            EmissionMode = emissionMode ?? CompanyEmissionMode.Electronic
        };

    private static async Task<VolterpDbContext> DbAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<VolterpDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new VolterpDbContext(options);
        await db.Database.EnsureCreatedAsync();

        return db;
    }
}
