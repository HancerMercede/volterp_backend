using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.Infrastructure.Data;
using Volterp.Infrastructure.Services;
using Volterp.Tests.Infrastructure;
using Volterp.Tests.TestHelpers;
using Xunit;

namespace Volterp.Tests.Integration;

// The whole thing end to end, against a real Postgres with the real
// migrations: the caller logs in, the API answers what it may do, the route
// it may use answers and the one it may not refuses.
//
// The other probes of the suite run on SQLite in memory. This one runs the
// migrations of the API on a disposable database and starts the API on top
// of it, because that is the only way to prove that the schema the
// migrations leave is the one the code expects.
[Collection("VolterpApiFactory")]
public class EndToEndAgainstPostgresTests
{
    private const string Password = "Passw0rd!";

    [PostgresFact]
    public async Task TheCallerLogsInAndTheApiAnswersWhatItMayDo()
    {
        var databaseName = PostgresSettings.DisposableDatabaseName();
        await PostgresSettings.CreateDisposableDatabaseAsync(databaseName);

        try
        {
            var connectionString = PostgresSettings.ConnectionStringOf(databaseName);

            await SeedAsync(connectionString);

            var factory = new ApiFactory(services =>
            {
                services.RemoveAll<DbContextOptions<VolterpDbContext>>();
                services.RemoveAll<DbContextOptions>();
                services.RemoveAll<VolterpDbContext>();
                services.RemoveAll<IDbContextOptionsConfiguration<VolterpDbContext>>();
                services.AddDbContext<VolterpDbContext>(options => options.UseNpgsql(connectionString));
            });

            using var client = factory.CreateClient();

            // The API reads the database the test seeded.
            using (var scope = factory.Services.CreateScope())
            {
                var apiDatabase = scope.ServiceProvider.GetRequiredService<VolterpDbContext>();
                var visible = await apiDatabase.Users.IgnoreQueryFilters()
                    .AnyAsync(candidate => candidate.Username == "e2e-user");

                visible.Should().BeTrue("the API reads the database the test seeded");
            }

            var login = await client.PostAsJsonAsync("/api/Auth/login",
                new { username = "e2e-user", password = Password });

            var loginBody = await login.Content.ReadAsStringAsync();

            login.StatusCode.Should().Be(HttpStatusCode.OK,
                $"the user was created with a password and the roles the matrix says: {loginBody}");

            using var loginDocument = JsonDocument.Parse(loginBody);
            var loginRoot = loginDocument.RootElement;

            loginRoot.GetProperty("roles").EnumerateArray().Select(role => role.GetString())
                .Should().BeEquivalentTo([nameof(UserRole.Ventas)]);

            var token = loginRoot.GetProperty("token").GetString()!;
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // What the caller may do comes from the API, already united.
            var me = await client.GetAsync("/api/permissions/me");
            me.StatusCode.Should().Be(HttpStatusCode.OK);

            using var meDocument = JsonDocument.Parse(await me.Content.ReadAsStringAsync());
            var permissions = meDocument.RootElement.GetProperty("permissions").EnumerateArray()
                .Select(permission => permission.GetString())
                .ToArray();

            permissions.Should().Contain("ventas.read");
            permissions.Should().NotContain("ventas.delete");

            // The route the role has, answers.
            var sales = await client.GetAsync("/api/Sales");
            sales.StatusCode.Should().Be(HttpStatusCode.OK);

            // The route the role does not have, refuses.
            var users = await client.GetAsync("/api/Users");
            users.StatusCode.Should().Be(HttpStatusCode.Forbidden);

            using var usersDocument = JsonDocument.Parse(await users.Content.ReadAsStringAsync());
            usersDocument.RootElement.GetProperty("error").GetString()
                .Should().Contain("configuracion.read",
                    "the refusal names the permission that is missing");
        }
        finally
        {
            await PostgresSettings.DropDisposableDatabaseAsync(databaseName);
        }
    }

    private static async Task SeedAsync(string connectionString)
    {
        var options = new DbContextOptionsBuilder<VolterpDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var context = new VolterpDbContext(options);

        // The migrations of the API, the same ones the client runs.
        await context.Database.MigrateAsync();

        var hasher = new PasswordHasher();

        var companyId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();

        context.Companies.Add(new Company
        {
            Id = companyId,
            Name = "Distribuidora del Caribe",
            TaxId = "101500099",
            Address = "Av. 27 de Febrero No. 42",
            LegalName = "Distribuidora del Caribe S.R.L.",
            Email = "contacto@distcaribe.do",
            Phone = "809-555-0101",
            IsActive = true
        });

        var user = new User
        {
            Id = userId,
            CompanyId = companyId,
            Code = "U-0001",
            Username = "e2e-user",
            Email = "e2e@distcaribe.do",
            FullName = "Vendedor de prueba",
            PasswordHash = hasher.Hash(Password),
            IsActive = true
        };

        user.AssignRole(UserRole.Ventas);

        context.Users.Add(user);

        await context.SaveChangesAsync();

        // The user is really there, with the roles and the password it has
        // to log in with.
        var saved = await context.Users.IgnoreQueryFilters()
            .Include(candidate => candidate.Roles)
            .SingleAsync(candidate => candidate.Username == "e2e-user");

        saved.Roles.Select(assignment => assignment.Role)
            .Should().BeEquivalentTo([UserRole.Ventas]);

        new PasswordHasher().Verify(Password, saved.PasswordHash).Should().BeTrue(
            "the password the test logs in with is the one the user has");
    }
}
