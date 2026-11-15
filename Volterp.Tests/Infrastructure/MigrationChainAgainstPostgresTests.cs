using System.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Volterp.Infrastructure.Data;
using Volterp.Tests.Migrations;
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

    public static async Task<string> CurrentDatabaseAsync(VolterpDbContext context)
    {
        var connection = context.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT current_database()";

        return (string)(await command.ExecuteScalarAsync())!;
    }

    public static async Task<List<string>> IndexesOf(VolterpDbContext context, string table)
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

    public static async Task<List<string>> TablesOf(VolterpDbContext context)
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

    public static async Task<List<string>> ColumnsOf(VolterpDbContext context, string table)
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

// The chain of the migrations, applied on a database that only exists for
// the probe: the model, the migrations and the SQL of the seeds have to work
// together on Postgres.
public class MigrationChainAgainstPostgresTests
{
    private static readonly string[] DemoTenantTaxIds =
    [
        "101500018",
        "101500026",
        "101500034",
        "101500042",
        "101500052"
    ];

    [PostgresFact]
    public async Task TheWholeChainAppliesOnPostgres()
    {
        var databaseName = PostgresSettings.DisposableDatabaseName();

        databaseName.Should().StartWith(
            "volterp_test_",
            "the migration test must never write to the development database");
        databaseName.Should().NotBe(
            PostgresSettings.ConfiguredDatabaseName,
            "the migration test must use a database other than the configured one");

        await PostgresSettings.CreateDisposableDatabaseAsync(databaseName);

        try
        {
            var options = new DbContextOptionsBuilder<VolterpDbContext>()
                .UseNpgsql(PostgresSettings.ConnectionStringOf(databaseName))
                .Options;

            await using var context = new VolterpDbContext(options);

            var currentDatabase = await PostgresSettings.CurrentDatabaseAsync(context);
            currentDatabase.Should().Be(
                databaseName,
                "the context must point to the disposable database");
            currentDatabase.Should().NotBe(
                "volterp",
                "the development database must never be touched by the migration test");

            var migrations = context.Database.GetMigrations().ToList();
            migrations.Should().HaveCountGreaterThan(
                1,
                "the chain needs at least one migration before the last one");

            var previousMigration = migrations[^2];
            var lastMigration = migrations[^1];

            // The last migration is held back for one turn so the schema that
            // existed before its Up can be compared with the result of its Down.
            // Every migration is still applied from zero, in order.
            await context.Database.MigrateAsync(previousMigration);
            var schemaBeforeLast = await PostgresSchemaSnapshot.CaptureAsync(context);

            await context.Database.MigrateAsync();
            var appliedMigrations = context.Database.GetAppliedMigrations().ToList();
            appliedMigrations.Should().Equal(
                migrations,
                "the chain must apply every Up migration discovered by Entity Framework");
            context.Database.GetPendingMigrations().Should().BeEmpty(
                "the chain must finish with every migration applied");

            var schemaAfterLast = await PostgresSchemaSnapshot.CaptureAsync(context);
            await AssertEcfSchemaAsync(context, schemaAfterLast);

            await context.Database.MigrateAsync(previousMigration);
            var schemaAfterRollback = await PostgresSchemaSnapshot.CaptureAsync(context);

            schemaAfterRollback.Tables.Should().Equal(
                schemaBeforeLast.Tables,
                "the Down of {0} must restore the tables that existed before its Up",
                lastMigration);
            schemaAfterRollback.Columns.Should().Equal(
                schemaBeforeLast.Columns,
                "the Down of {0} must restore the columns that existed before its Up",
                lastMigration);
            schemaAfterRollback.Indexes.Should().Equal(
                schemaBeforeLast.Indexes,
                "the Down of {0} must restore the indexes that existed before its Up",
                lastMigration);

            await context.Database.MigrateAsync();
            var schemaAfterReapply = await PostgresSchemaSnapshot.CaptureAsync(context);

            schemaAfterReapply.Tables.Should().Equal(
                schemaAfterLast.Tables,
                "the Up of {0} must restore the tables it changed",
                lastMigration);
            schemaAfterReapply.Columns.Should().Equal(
                schemaAfterLast.Columns,
                "the Up of {0} must restore the columns it changed",
                lastMigration);
            schemaAfterReapply.Indexes.Should().Equal(
                schemaAfterLast.Indexes,
                "the Up of {0} must restore the indexes it changed",
                lastMigration);
        }
        finally
        {
            await PostgresSettings.DropDisposableDatabaseAsync(databaseName);
        }
    }

    private static async Task AssertEcfSchemaAsync(
        VolterpDbContext context,
        PostgresSchemaSnapshot schema)
    {
        var tables = await PostgresSettings.TablesOf(context);
        tables.Should().Contain(
            new[] { "ElectronicInvoices", "ElectronicInvoiceSequences" },
            "the e-CF tables must exist after the migrations run");

        var invoiceColumns = await PostgresSettings.ColumnsOf(context, "ElectronicInvoices");
        invoiceColumns.Should().Contain(
            new[]
            {
                "ENCF", "DocumentType", "IssuerTaxId", "ReceiverTaxId",
                "SignedXml", "SignedAt", "TrackId", "SecurityCode", "QrUrl",
                "SentAt", "Status", "VerdictNote"
            },
            "the e-CF document must keep the data needed to emit and follow the document");
        invoiceColumns.Should().Contain(
            new[]
            {
                "OriginalXml", "AttemptCount", "LeaseOwner", "LeaseUntil",
                "NextAttemptAt", "RequiresReview"
            },
            "the durable e-CF queue must keep its retry, lease and review columns");
        invoiceColumns.Should().Contain(
            new[] { "NextCheckAt", "StatusCheckCount" },
            "the retrack of a pending verdict must keep its appointment and counter");
        invoiceColumns.Should().Contain(
            new[] { "LastError", "RawResponse", "ReceivedAt", "SequenceConsumed" },
            "the answer of the DGII must be kept with the document");

        var sequenceColumns = await PostgresSettings.ColumnsOf(context, "ElectronicInvoiceSequences");
        sequenceColumns.Should().Contain(
            new[] { "CompanyId", "IssuerTaxId", "DocumentType", "From", "To", "ExpiresAt", "CurrentSequence" },
            "an authorized e-NCF range must keep its issuer, bounds, expiry and position");

        schema.Indexes.Should().ContainSingle(
            index => index.Name == "IX_ElectronicInvoices_IssuerTaxId_ENCF"
                && index.Definition.Contains("CREATE UNIQUE INDEX", StringComparison.Ordinal)
                && index.Definition.Contains("\"IssuerTaxId\", \"ENCF\"", StringComparison.Ordinal),
            "the same e-NCF cannot exist twice for the same issuer");
        schema.Indexes.Should().ContainSingle(
            index => index.Name == "IX_ElectronicInvoices_SaleId"
                && index.Definition.Contains("CREATE UNIQUE INDEX", StringComparison.Ordinal)
                && index.Definition.Contains("\"SaleId\"", StringComparison.Ordinal),
            "a sale cannot have two fiscal documents");
        schema.Indexes.Should().ContainSingle(
            index => index.Name == "IX_ElectronicInvoiceSequences_IssuerTaxId_DocumentType_From"
                && index.Definition.Contains("CREATE UNIQUE INDEX", StringComparison.Ordinal)
                && index.Definition.Contains("\"IssuerTaxId\", \"DocumentType\", \"From\"", StringComparison.Ordinal),
            "the same authorized range cannot exist twice for an issuer and document type");

        var demoTenantTaxIds = await DemoTenantTaxIdsAsync(context);
        demoTenantTaxIds.Should().Equal(
            DemoTenantTaxIds,
            "the migrations must leave the five demo tenants in the database");
    }

    private static async Task<List<string>> DemoTenantTaxIdsAsync(VolterpDbContext context)
    {
        var connection = context.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT "TaxId"
            FROM "Companies"
            WHERE "TaxId" IN ('101500018', '101500026', '101500034', '101500042', '101500052')
            ORDER BY "TaxId"
            """;

        var taxIds = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
            taxIds.Add(reader.GetString(0));

        return taxIds;
    }
}
