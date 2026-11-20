using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.Infrastructure.Data;
using Xunit;

namespace Volterp.Tests.Infrastructure;

// The backfill of the roles: when the column that carried the role of a user
// goes away, the assignment that replaces it has to say the same thing. The
// probe needs a real Postgres: it walks the chain of the migrations.
public class UserRolesBackfillTests
{
    private const string MigrationName = "AddUserRoles";

    [PostgresFact]
    public async Task TheUserKeepsTheRoleItHadBeforeTheColumnWentAway()
    {
        var databaseName = PostgresSettings.DisposableDatabaseName();
        await PostgresSettings.CreateDisposableDatabaseAsync(databaseName);

        try
        {
            var options = new DbContextOptionsBuilder<VolterpDbContext>()
                .UseNpgsql(PostgresSettings.ConnectionStringOf(databaseName))
                .Options;

            await using var context = new VolterpDbContext(options);

            var migrations = context.Database.GetMigrations().ToList();
            var index = migrations.FindIndex(name => name.EndsWith($"_{MigrationName}"));

            index.Should().BeGreaterThan(0,
                "the migration of the roles has to be in the chain, and something has to come before it");

            await context.Database.MigrateAsync(migrations[index - 1]);

            var companyId = Guid.CreateVersion7();
            var userId = Guid.CreateVersion7();

            context.Companies.Add(new Company
            {
                Id = companyId,
                Name = "Company",
                TaxId = "130862346",
                Address = "1 A Street",
                LegalName = "Company Legal",
                Email = "a@company.test",
                Phone = "809-555-1234",
                IsActive = true
            });
            await context.SaveChangesAsync();

            // The user is written with SQL because the model no longer has the
            // column: at this point of the chain the column is still there.
            await context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO "Users" ("Id", "CompanyId", "Code", "Username", "PasswordHash", "Email", "FullName", "Role", "IsActive", "CreatedAt")
                VALUES ({0}, {1}, 'U-0001', 'backfill-user', 'hash', 'backfill@company.test', 'Backfill User', {2}, true, now());
                """,
                userId,
                companyId,
                (int)UserRole.Inventario);

            await context.Database.MigrateAsync();

            var roles = await RolesOfAsync(context, userId);

            roles.Should().ContainSingle().Which.Should().Be((int)UserRole.Inventario,
                "the user keeps the role it had: the column is gone and the assignment says the same thing");

            var columns = await PostgresSettings.ColumnsOf(context, "Users");
            columns.Should().NotContain("Role", "the column that carried the role is gone");
        }
        finally
        {
            await PostgresSettings.DropDisposableDatabaseAsync(databaseName);
        }
    }

    private static async Task<List<int>> RolesOfAsync(VolterpDbContext context, Guid userId)
    {
        var connection = context.Database.GetDbConnection();

        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT \"Role\" FROM \"UserRoles\" WHERE \"UserId\" = @userId";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "userId";
        parameter.Value = userId;
        command.Parameters.Add(parameter);

        var roles = new List<int>();

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
            roles.Add(reader.GetInt32(0));

        return roles;
    }
}
