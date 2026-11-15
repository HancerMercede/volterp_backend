using System.Data;
using System.Data.Common;
using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql;
using Volterp.Infrastructure.Data;
using Volterp.Infrastructure.Migrations;
using Volterp.Tests.Infrastructure;
using Xunit;

namespace Volterp.Tests.Migrations;

public class T51EcfRangeRollbackTests
{
    private const string T25 = "20261113120000_AddEcfRangesAndNoteReferences";
    private const string BeforeT25 = "20261105222338_AddBusinessDateToClientsAndProducts";

    private static readonly Guid DemoCompanyId = new("0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e01");
    private const string DemoIssuerTaxId = "101500018";
    private const int DocumentType = 41;

    [Fact]
    public void TheDownOfT25RefusesSeveralRangesAndRestoresTheUniqueOldIndex()
    {
        var migration = new AddEcfRangesAndNoteReferences();
        var operations = DownOperationsOf(migration);

        var guards = operations
            .OfType<SqlOperation>()
            .Where(operation => operation.Sql.Contains("RAISE EXCEPTION", StringComparison.Ordinal))
            .ToList();

        guards.Should().ContainSingle(
            "the Down must refuse a database that has more than one range for a company and document type");
        guards[0].Sql.Should().Contain("one range per company and document type");

        var oldIndexCreations = operations
            .OfType<CreateIndexOperation>()
            .Where(operation => operation.Columns
                .SequenceEqual(["CompanyId", "DocumentType"]))
            .ToList();

        oldIndexCreations.Should().ContainSingle(
            "the Down must restore the index that existed before T25");
        oldIndexCreations[0].IsUnique.Should().BeTrue(
            "the old allocation statement used ON CONFLICT (CompanyId, DocumentType)");
        oldIndexCreations.Should().NotContain(
            operation => !operation.IsUnique,
            "the Down must never leave the old index non-unique");
    }

    [PostgresFact]
    public async Task TheRollbackFailsWithoutChangingTheDatabaseWhenTwoRangesShareThePair()
    {
        var databaseName = PostgresSettings.DisposableDatabaseName();
        await PostgresSettings.CreateDisposableDatabaseAsync(databaseName);

        try
        {
            var options = OptionsFor(databaseName);
            DatabaseFingerprint before;
            List<string> historyBefore;

            await using (var context = new VolterpDbContext(options))
            {
                var migrator = context.GetService<IMigrator>();
                await migrator.MigrateAsync(T25);

                await InsertRangeAsync(context, new Guid("0192b9c7-b6a3-7def-8c5e-1f2a3b4c5f01"), 1);
                await InsertRangeAsync(context, new Guid("0192b9c7-b6a3-7def-8c5e-1f2a3b4c5f02"), 2);

                before = await FingerprintAsync(context);
                historyBefore = context.Database.GetAppliedMigrations().ToList();
                historyBefore.Last().Should().Be(
                    T25,
                    "the chain must be at T25 before its rollback is attempted");

                var exception = await Assert.ThrowsAsync<PostgresException>(
                    () => migrator.MigrateAsync(BeforeT25));

                exception.SqlState.Should().Be(
                    "P0001",
                    "the guard in the Down must raise the Postgres error that explains the rollback");
                exception.MessageText.Should().Contain(
                    "one range per company and document type",
                    "the operator must receive the reason why the rollback cannot continue");
            }

            await using var verification = new VolterpDbContext(options);
            var after = await FingerprintAsync(verification);

            after.IndexesHash.Should().Be(
                before.IndexesHash,
                "the failed rollback must leave every index unchanged");
            after.SchemaHash.Should().Be(
                before.SchemaHash,
                "the failed rollback must leave information_schema unchanged");
            after.RowsHash.Should().Be(
                before.RowsHash,
                "the failed rollback must leave every range row unchanged");
            verification.Database.GetAppliedMigrations().Should().Equal(
                historyBefore,
                "the failed rollback must leave the migration history unchanged");
            verification.Database.GetAppliedMigrations().Last().Should().Be(
                T25,
                "T25 must still be the last applied migration after its failed rollback");
        }
        finally
        {
            await PostgresSettings.DropDisposableDatabaseAsync(databaseName);
        }
    }

    [PostgresFact]
    public async Task TheOldAllocationStillWorksAfterTheRollbackAndT25CanBeAppliedAgain()
    {
        var databaseName = PostgresSettings.DisposableDatabaseName();
        await PostgresSettings.CreateDisposableDatabaseAsync(databaseName);

        try
        {
            var options = OptionsFor(databaseName);
            await using var context = new VolterpDbContext(options);
            var migrator = context.GetService<IMigrator>();

            await migrator.MigrateAsync(T25);
            await InsertRangeAsync(context, new Guid("0192b9c7-b6a3-7def-8c5e-1f2a3b4c5f03"), 1);

            await migrator.MigrateAsync(BeforeT25);

            var indexesAfterRollback = await PostgresSettings.IndexesOf(
                context,
                "ElectronicInvoiceSequences");
            indexesAfterRollback.Should().ContainSingle(
                index => index.Contains("IX_ElectronicInvoiceSequences_CompanyId_DocumentType", StringComparison.Ordinal)
                    && index.Contains("CREATE UNIQUE INDEX", StringComparison.Ordinal)
                    && index.Contains("\"CompanyId\", \"DocumentType\"", StringComparison.Ordinal),
                "the rollback must restore the unique index used by the old allocation statement");

            var sequence = await ExecuteHistoricalAllocationAsync(context);
            sequence.Should().Be(
                1,
                "the old ON CONFLICT statement must update the existing range instead of failing");

            await migrator.MigrateAsync(T25);

            var indexesAfterReapply = await PostgresSettings.IndexesOf(
                context,
                "ElectronicInvoiceSequences");
            indexesAfterReapply.Should().ContainSingle(
                index => index.Contains("IX_ElectronicInvoiceSequences_IssuerTaxId_DocumentType_From", StringComparison.Ordinal)
                    && index.Contains("CREATE UNIQUE INDEX", StringComparison.Ordinal)
                    && index.Contains("\"IssuerTaxId\", \"DocumentType\", \"From\"", StringComparison.Ordinal),
                "T25 must restore the unique index of the authorized ranges");
            indexesAfterReapply.Should().NotContain(
                index => index.Contains("IX_ElectronicInvoiceSequences_CompanyId_DocumentType", StringComparison.Ordinal),
                "the old unique index must not survive the re-apply of T25");
        }
        finally
        {
            await PostgresSettings.DropDisposableDatabaseAsync(databaseName);
        }
    }

    private static DbContextOptions<VolterpDbContext> OptionsFor(string databaseName)
        => new DbContextOptionsBuilder<VolterpDbContext>()
            .UseNpgsql(PostgresSettings.ConnectionStringOf(databaseName))
            .Options;

    private static async Task InsertRangeAsync(
        VolterpDbContext context,
        Guid id,
        int from)
    {
        var connection = context.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO "ElectronicInvoiceSequences"
            (
                "Id", "CompanyId", "IssuerTaxId", "DocumentType", "From", "To",
                "ExpiresAt", "CurrentSequence", "CreatedAt"
            )
            VALUES
            (
                @id, @companyId, @issuerTaxId, @documentType, @from, @to,
                @expiresAt, 0, @createdAt
            )
            """;

        AddParameter(command, "id", id);
        AddParameter(command, "companyId", DemoCompanyId);
        AddParameter(command, "issuerTaxId", DemoIssuerTaxId);
        AddParameter(command, "documentType", DocumentType);
        AddParameter(command, "from", from);
        AddParameter(command, "to", from + 100);
        AddParameter(command, "expiresAt", new DateTime(2028, 12, 31, 23, 59, 59, DateTimeKind.Utc));
        AddParameter(command, "createdAt", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> ExecuteHistoricalAllocationAsync(VolterpDbContext context)
    {
        var connection = context.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO "ElectronicInvoiceSequences"
            ("Id", "CompanyId", "DocumentType", "CurrentSequence", "CreatedAt")
            VALUES
            (@id, @companyId, @documentType, 1, @createdAt)
            ON CONFLICT ("CompanyId", "DocumentType")
            DO UPDATE SET "CurrentSequence" = "ElectronicInvoiceSequences"."CurrentSequence" + 1
            RETURNING "CurrentSequence"
            """;

        AddParameter(command, "id", new Guid("0192b9c7-b6a3-7def-8c5e-1f2a3b4c5f04"));
        AddParameter(command, "companyId", DemoCompanyId);
        AddParameter(command, "documentType", DocumentType);
        AddParameter(command, "createdAt", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        object? result = null;
        var exception = await Record.ExceptionAsync(async () =>
        {
            result = await command.ExecuteScalarAsync();
        });

        exception.Should().BeNull(
            "the historical allocation statement must not fail with SQLSTATE 42P10; found {0}",
            exception?.Message);

        return Convert.ToInt64(result);
    }

    private static async Task<DatabaseFingerprint> FingerprintAsync(VolterpDbContext context)
    {
        var connection = context.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        var indexesHash = await ScalarStringAsync(
            connection,
            """
            SELECT md5(COALESCE(string_agg(indexdef, E'\n' ORDER BY tablename, indexname), ''))
            FROM pg_indexes
            WHERE schemaname = 'public'
            """);

        var schemaHash = await ScalarStringAsync(
            connection,
            """
            SELECT md5(COALESCE(string_agg(
                table_name || ':' || column_name || ':' || data_type || ':' || udt_name || ':' ||
                is_nullable || ':' || COALESCE(character_maximum_length::text, '') || ':' ||
                COALESCE(numeric_precision::text, '') || ':' || COALESCE(numeric_scale::text, '') || ':' ||
                COALESCE(datetime_precision::text, '') || ':' || COALESCE(column_default, ''),
                E'\n' ORDER BY table_name, ordinal_position), ''))
            FROM information_schema.columns
            WHERE table_schema = 'public'
            """);

        var rowsHash = await ScalarStringAsync(
            connection,
            """
            SELECT md5(COALESCE(string_agg(
                row_to_json(ranges)::text,
                E'\n' ORDER BY row_to_json(ranges)::text), ''))
            FROM "ElectronicInvoiceSequences" AS ranges
            """);

        return new DatabaseFingerprint(indexesHash, schemaHash, rowsHash);
    }

    private static async Task<string> ScalarStringAsync(
        DbConnection connection,
        string commandText)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;

        return (string)(await command.ExecuteScalarAsync())!;
    }

    private static IReadOnlyList<MigrationOperation> DownOperationsOf(Migration migration)
    {
        var builder = Builder();
        var down = migration.GetType().GetMethod("Down", BindingFlags.Instance | BindingFlags.NonPublic);

        down.Should().NotBeNull("T25 must have a Down operation");
        down!.Invoke(migration, [builder]);

        return builder.Operations;
    }

    private static MigrationBuilder Builder()
        => new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;

        command.Parameters.Add(parameter);
    }

    private sealed record DatabaseFingerprint(
        string IndexesHash,
        string SchemaHash,
        string RowsHash);
}
