using System.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Volterp.Infrastructure.Data;
using Xunit;

namespace Volterp.Tests.Infrastructure;

// The chain of the migrations applied on a real Postgres, on a database of
// its own: the SQL of the seeds is Postgres and SQLite cannot run it.
//
// The test is skipped when there is no Postgres, or when the role cannot
// create the database of the probe.
public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (!PostgresSettings.CanCreateDatabases)
            Skip = "There is no Postgres where the chain of the migrations can be applied.";
    }
}

// Where the probes that need Postgres find it, and how they get a database
// of their own.
internal static class PostgresSettings
{
    private const string DefaultConnectionString =
        "Host=localhost;Database=volterp;Username=volterp;Password=temppassword123;Port=5432";

    public static string ConnectionString =>
        Environment.GetEnvironmentVariable("VOLTERP_CONNECTION_STRING") ?? DefaultConnectionString;

    public static bool CanCreateDatabases
    {
        get
        {
            try
            {
                var builder = new NpgsqlConnectionStringBuilder(ConnectionString) { Timeout = 3 };

                using var connection = new NpgsqlConnection(builder.ConnectionString);
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = "SELECT rolcreatedb FROM pg_roles WHERE rolname = current_user";

                return (bool?)command.ExecuteScalar() == true;
            }
            catch
            {
                return false;
            }
        }
    }

    public static string ConnectionStringOf(string databaseName)
        => new NpgsqlConnectionStringBuilder(ConnectionString)
        {
            Database = databaseName
        }.ConnectionString;

    public static string AdminConnectionString()
        => new NpgsqlConnectionStringBuilder(ConnectionString)
        {
            Database = "postgres"
        }.ConnectionString;

    public static async Task CreateDatabaseAsync(string databaseName)
    {
        await using var connection = new NpgsqlConnection(AdminConnectionString());
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE \"{databaseName}\"";
        await command.ExecuteNonQueryAsync();
    }

    public static async Task DropDatabaseAsync(string databaseName)
    {
        NpgsqlConnection.ClearAllPools();

        await using var connection = new NpgsqlConnection(AdminConnectionString());
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)";
        await command.ExecuteNonQueryAsync();
    }

    public static async Task<List<string>> IndexesOf(VolterpDbContext context, string table)
    {
        var connection = context.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT indexdef FROM pg_indexes WHERE tablename = @table";
        AddParameter(command, "table", table);

        var indexes = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
            indexes.Add(reader.GetString(0));

        return indexes;
    }

    public static async Task<List<string>> TablesOf(VolterpDbContext context)
    {
        var connection = context.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'";

        var tables = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
            tables.Add(reader.GetString(0));

        return tables;
    }

    private static void AddParameter(System.Data.Common.DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;

        command.Parameters.Add(parameter);
    }
}

// The chain of the migrations, applied on a database that only exists for
// the probe: the model, the migrations and the SQL of the seeds have to work
// together on Postgres.
public class MigrationChainAgainstPostgresTests
{
    [PostgresFact]
    public async Task TheWholeChainAppliesOnPostgres()
    {
        var databaseName = $"volterp_migration_probe_{Guid.NewGuid():N}"[..40];

        await PostgresSettings.CreateDatabaseAsync(databaseName);

        try
        {
            var options = new DbContextOptionsBuilder<VolterpDbContext>()
                .UseNpgsql(PostgresSettings.ConnectionStringOf(databaseName))
                .Options;

            await using var context = new VolterpDbContext(options);

            await context.Database.MigrateAsync();

            var indexes = await PostgresSettings.IndexesOf(context, "ElectronicInvoices");

            indexes.Should().Contain(index => index.Contains("UNIQUE") && index.Contains("ENCF"),
                "the e-NCF cannot exist twice for the same issuer");
            indexes.Should().Contain(index => index.Contains("UNIQUE") && index.Contains("SaleId"),
                "a sale cannot have two fiscal documents");

            var tables = await PostgresSettings.TablesOf(context);

            tables.Should().Contain("ElectronicInvoices");
            tables.Should().Contain("ElectronicInvoiceSequences");
        }
        finally
        {
            await PostgresSettings.DropDatabaseAsync(databaseName);
        }
    }
}
