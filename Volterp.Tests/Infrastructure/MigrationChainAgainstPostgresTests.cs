using System.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Volterp.Infrastructure.Data;
using Volterp.Tests.Migrations;
using Xunit;

namespace Volterp.Tests.Infrastructure;

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
            schemaAfterRollback.Columns
                .Select(column => column with { Position = 0 })
                .Should().BeEquivalentTo(
                    schemaBeforeLast.Columns.Select(column => column with { Position = 0 }),
                    "the Down of {0} must restore the columns that existed before its Up. " +
                    "The physical position of a column is not part of the shape of the table: " +
                    "Postgres always appends the column that is added back, so a column that was " +
                    "dropped from the middle of a table comes back last. The columns have to be the same.",
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

    // The databases of the installations that ran the app before the model
    // changed still have the tables of the accounts and of the journal. They
    // are not in the model anymore, but their rows point at the companies with
    // a restrictive foreign key: the mock company cannot be deleted while
    // they are there, and the chain stops with a foreign key violation.
    [PostgresFact]
    public async Task TheChainAppliesOnADatabaseThatStillHasTheLegacyTables()
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
            var removeMockIndex = migrations.FindIndex(
                name => name.EndsWith("RemoveMockSeedData", StringComparison.Ordinal));

            removeMockIndex.Should().BeGreaterThan(
                0,
                "the chain must have a migration before the one that takes the mock company away");

            await context.Database.MigrateAsync(migrations[removeMockIndex - 1]);

            await ExecuteAsync(
                context,
                """
                CREATE TABLE "Accounts" (
                    "Id" uuid NOT NULL DEFAULT gen_random_uuid(),
                    "CompanyId" uuid NOT NULL,
                    CONSTRAINT "PK_Accounts" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_Accounts_Companies_CompanyId"
                        FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id")
                );
                CREATE TABLE "JournalEntries" (
                    "Id" uuid NOT NULL DEFAULT gen_random_uuid(),
                    "CompanyId" uuid NOT NULL,
                    CONSTRAINT "PK_JournalEntries" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_JournalEntries_Companies_CompanyId"
                        FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id")
                );
                CREATE TABLE "JournalLines" (
                    "Id" uuid NOT NULL DEFAULT gen_random_uuid(),
                    "CompanyId" uuid NOT NULL,
                    CONSTRAINT "PK_JournalLines" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_JournalLines_Companies_CompanyId"
                        FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id")
                );
                INSERT INTO "Accounts" ("CompanyId")
                    VALUES ('00000001-0000-0000-0000-000000000001');
                INSERT INTO "JournalEntries" ("CompanyId")
                    VALUES ('00000001-0000-0000-0000-000000000001');
                INSERT INTO "JournalLines" ("CompanyId")
                    VALUES ('00000001-0000-0000-0000-000000000001');
                INSERT INTO "Accounts" ("CompanyId")
                    SELECT "Id" FROM "Companies" WHERE "TaxId" = '101500018';
                """);

            await context.Database.MigrateAsync();

            context.Database.GetPendingMigrations().Should().BeEmpty(
                "the chain must apply even on a database that still has the tables of the old model");

            (await CountAsync(
                context,
                "SELECT count(*) FROM \"Companies\" WHERE \"Id\" = '00000001-0000-0000-0000-000000000001';"))
                .Should().Be(
                    0,
                    "the mock company must be gone");

            foreach (var legacyTable in new[] { "Accounts", "JournalEntries", "JournalLines" })
            {
                (await CountAsync(context, MockCompanyRows(legacyTable))).Should().Be(
                    0,
                    "the rows of the mock company must go with it in {0}", legacyTable);
            }

            (await CountAsync(
                context,
                "SELECT count(*) FROM \"Accounts\" WHERE \"CompanyId\" <> '00000001-0000-0000-0000-000000000001';"))
                .Should().Be(
                    1,
                    "the rows of the other tenants must stay where they are");

            (await CountAsync(
                context,
                "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'Accounts';"))
                .Should().Be(
                    1,
                    "the tables of the old model are not ours to drop");
        }
        finally
        {
            await PostgresSettings.DropDisposableDatabaseAsync(databaseName);
        }
    }

    private static string MockCompanyRows(string table)
        => $"SELECT count(*) FROM \"{table}\" WHERE \"CompanyId\" = '00000001-0000-0000-0000-000000000001';";

    private static async Task ExecuteAsync(VolterpDbContext context, string sql)
    {
        var connection = context.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> CountAsync(VolterpDbContext context, string sql)
    {
        var connection = context.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        return (long)(await command.ExecuteScalarAsync())!;
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
