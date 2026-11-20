using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.Infrastructure.Data;
using Volterp.Infrastructure.Repositories;
using Volterp.Tests.Infrastructure;
using Xunit;

namespace Volterp.Tests.Onboarding;

// The criterion of the lookup by email is pinned against Postgres, the
// provider of the database of the clients: the address of a user is found
// whatever the case it is asked with, and the comparison is one that both
// providers translate into SQL.
public class UserEmailLookupTests
{
    [PostgresFact]
    public async Task TheUserIsFoundByItsEmailWhateverTheCase()
    {
        var databaseName = PostgresSettings.DisposableDatabaseName();

        await PostgresSettings.CreateDisposableDatabaseAsync(databaseName);

        try
        {
            await using var context = await ContextAsync(databaseName);

            var company = await CompanyAsync(context);
            var user = await UserAsync(context, company.Id, "Usu-001", "Admin@Volterp.Do");

            var repository = new UserRepository(context);

            (await repository.GetByUserByEmailAsync("Admin@Volterp.Do")).Should().NotBeNull(
                "the user is found when the address is the one it has");
            (await repository.GetByUserByEmailAsync("admin@volterp.do")).Should().NotBeNull(
                "the address of a user is compared without case");
            (await repository.GetByUserByEmailAsync("ADMIN@VOLTERP.DO")).Should().NotBeNull(
                "the address of a user is compared without case, in any case");
            (await repository.GetByUserByEmailAsync("otro@volterp.do")).Should().BeNull(
                "the address of another user is not the address of this one");
        }
        finally
        {
            await PostgresSettings.DropDisposableDatabaseAsync(databaseName);
        }
    }

    [PostgresFact]
    public async Task TheUniqueIndexOfTheEmailIsCaseSensitive()
    {
        var databaseName = PostgresSettings.DisposableDatabaseName();

        await PostgresSettings.CreateDisposableDatabaseAsync(databaseName);

        try
        {
            await using var context = await ContextAsync(databaseName);

            var company = await CompanyAsync(context);

            await UserAsync(context, company.Id, "Usu-001", "Admin@Volterp.Do");
            await UserAsync(context, company.Id, "Usu-002", "admin@volterp.do");

            // The database takes the two addresses: its unique index compares
            // them with case. That is why the command checks the address of the
            // administrator by itself, so the same address does not pass in
            // another case.
            (await context.Users.CountAsync()).Should().Be(2,
                "the unique index of the email compares the addresses with case");
        }
        finally
        {
            await PostgresSettings.DropDisposableDatabaseAsync(databaseName);
        }
    }

    private static async Task<VolterpDbContext> ContextAsync(string databaseName)
    {
        var options = new DbContextOptionsBuilder<VolterpDbContext>()
            .UseNpgsql(PostgresSettings.ConnectionStringOf(databaseName))
            .Options;

        var context = new VolterpDbContext(options);
        await context.Database.EnsureCreatedAsync();

        return context;
    }

    private static async Task<Company> CompanyAsync(VolterpDbContext context)
    {
        var company = new Company
        {
            Id = Guid.CreateVersion7(),
            Name = "Distribuidora del Caribe",
            TaxId = "101500018",
            Address = "Av. 27 de Febrero No. 42",
            LegalName = "Distribuidora del Caribe S.R.L.",
            Phone = "809-555-0101",
            Email = "contacto@distcaribe.do"
        };

        await context.Companies.AddAsync(company);
        await context.SaveChangesAsync();

        return company;
    }

    private static async Task<User> UserAsync(
        VolterpDbContext context,
        Guid companyId,
        string code,
        string email)
    {
        var user = new User
        {
            Id = Guid.CreateVersion7(),
            Code = code,
            Username = $"admin{code}",
            Email = email,
            FullName = "Administrador",
            PasswordHash = "$2a$12$hash",
            CompanyId = companyId
        };

        user.AssignRole(UserRole.SuperAdmin);

        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();

        return user;
    }
}
