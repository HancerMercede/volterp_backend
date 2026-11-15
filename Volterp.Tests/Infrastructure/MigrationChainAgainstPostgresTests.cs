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
