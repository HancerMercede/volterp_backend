using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Volterp.Infrastructure.Data;
using Xunit;

namespace Volterp.Tests.Infrastructure;

// The test is skipped when there is no Postgres, or when the role cannot
// create the database of the probe.
public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (!PostgresSettings.CanCreateDatabases)
            Skip = "There is no Postgres where the probe can run.";
    }
}

// Where the probes that need Postgres find it, and how they get a database
// of their own.
internal static class PostgresSettings
{
    private const string DefaultConnectionString =
        "Host=localhost;Database=volterp;Username=volterp;Password=temppassword123;Port=5432";

    private const string DisposableDatabasePrefix = "volterp_test_";

    public static string ConnectionString =>
        Environment.GetEnvironmentVariable("VOLTERP_CONNECTION_STRING") ?? DefaultConnectionString;

    public static string ConfiguredDatabaseName =>
        new NpgsqlConnectionStringBuilder(ConnectionString).Database ?? string.Empty;

    public static bool CanCreateDatabases
    {
        get
        {
            try
            {
                var builder = new NpgsqlConnectionStringBuilder(ConnectionString)
                {
                    Timeout = 3,
                    Database = "postgres"
                };

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

    public static string DisposableDatabaseName()
        => $"{DisposableDatabasePrefix}{Guid.NewGuid():N}";

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

    public static async Task CreateDisposableDatabaseAsync(string databaseName)
    {
        EnsureDisposableDatabaseName(databaseName);
        await CreateDatabaseAsync(databaseName);
    }

    public static async Task DropDisposableDatabaseAsync(string databaseName)
    {
        EnsureDisposableDatabaseName(databaseName);
        await DropDatabaseAsync(databaseName);

        if (await DatabaseExistsAsync(databaseName))
        {
            throw new InvalidOperationException(
                $"The disposable database '{databaseName}' still exists after it was dropped.");
        }
    }

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

    public static async Task<bool> DatabaseExistsAsync(string databaseName)
    {
        await using var connection = new NpgsqlConnection(AdminConnectionString());
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @name)";
        AddParameter(command, "name", databaseName);

        return (bool)(await command.ExecuteScalarAsync())!;
    }

    public static async Task<string> CurrentDatabaseAsync(Volterp.Infrastructure.Data.VolterpDbContext context)
    {
        var connection = context.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT current_database()";

        return (string)(await command.ExecuteScalarAsync())!;
    }

    public static async Task<List<string>> IndexesOf(Volterp.Infrastructure.Data.VolterpDbContext context, string table)
    {
        var connection = context.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT indexdef
            FROM pg_indexes
            WHERE schemaname = 'public'
              AND tablename = @table
            ORDER BY indexname
            """;

        AddParameter(command, "table", table);

        var indexes = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
            indexes.Add(reader.GetString(0));

        return indexes;
    }

    public static async Task<List<string>> TablesOf(Volterp.Infrastructure.Data.VolterpDbContext context)
    {
        var connection = context.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = 'public'
              AND table_type = 'BASE TABLE'
            ORDER BY table_name
            """;

        var tables = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
            tables.Add(reader.GetString(0));

        return tables;
    }

    public static async Task<List<string>> ColumnsOf(Volterp.Infrastructure.Data.VolterpDbContext context, string table)
    {
        var connection = context.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT column_name
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = @table
            ORDER BY ordinal_position
            """;

        AddParameter(command, "table", table);

        var columns = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
            columns.Add(reader.GetString(0));

        return columns;
    }

    private static void EnsureDisposableDatabaseName(string databaseName)
    {
        var hasOnlySafeCharacters = databaseName.All(character =>
            character == '_' || char.IsLetterOrDigit(character));

        if (!databaseName.StartsWith(DisposableDatabasePrefix, StringComparison.Ordinal)
            || databaseName.Length > 63
            || !hasOnlySafeCharacters
            || string.Equals(databaseName, ConfiguredDatabaseName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The disposable database must be a new database whose name starts with '{DisposableDatabasePrefix}'.");
        }
    }

    private static void AddParameter(System.Data.Common.DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;

        command.Parameters.Add(parameter);
    }
}
