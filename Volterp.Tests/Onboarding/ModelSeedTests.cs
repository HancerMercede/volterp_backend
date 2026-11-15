using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Volterp.Domain.Entities;
using Volterp.Infrastructure.Data;
using Xunit;

namespace Volterp.Tests.Onboarding;

// The model of a client has to be clean: the mock company and the mock
// administrator that the model used to seed are gone, so every new database
// is born empty and the tenant of the client arrives with its own identity.
public class ModelSeedTests
{
    private const string ConnectionString =
        "Host=localhost;Database=volterp;Username=volterp;Password=temppassword123;Port=5432";

    [Fact]
    public void TheDesignTimeModelDoesNotSeedTheMockCompany()
    {
        using var context = Context();

        var company = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Company));

        company.Should().NotBeNull();
        company!.GetSeedData().Should().BeEmpty(
            "the company of the mock is not a company of any client: it has a fake RNC and it is seeded in every new database");
    }

    [Fact]
    public void TheDesignTimeModelDoesNotSeedTheMockAdmin()
    {
        using var context = Context();

        var user = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(User));

        user.Should().NotBeNull();
        user!.GetSeedData().Should().BeEmpty(
            "the administrator of the mock has a published password: it cannot be seeded in the database of a client");
    }

    [Fact]
    public async Task ANewDatabaseCreatedFromTheModelHasNoCompanyAndNoUser()
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<VolterpDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new VolterpDbContext(options);

        await context.Database.EnsureCreatedAsync();

        (await context.Companies.CountAsync()).Should().Be(0,
            "a database that is born with a company is a database that is born with the tenant of somebody else");
        (await context.Users.CountAsync()).Should().Be(0);
    }

    private static VolterpDbContext Context()
    {
        var options = new DbContextOptionsBuilder<VolterpDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new VolterpDbContext(options);
    }
}
