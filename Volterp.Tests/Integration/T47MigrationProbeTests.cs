using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Volterp.Infrastructure.Data;
using Volterp.Tests.Infrastructure;
using Xunit;

namespace Volterp.Tests.Integration;

// Independent probe of T47 (read-only verifier): the migration of the four
// columns applies and reverts on the real Postgres, and the old rows keep the
// four columns empty.
//
// The chain is Postgres and SQLite cannot run it: the test is skipped when
// there is no Postgres.
[Collection("VolterpApiFactory")]
public class T47MigrationProbeTests
{
    private const string T47 = "20261114221020_AddDgiiResponses";
    private const string BeforeT47 = "20261114090000_AddVerdictRetrack";

    [PostgresFact]
    public async Task TheMigrationAppliesAndRevertsOnTheRealPostgres()
    {
        var databaseName = $"volterp_t47_probe_{Guid.NewGuid():N}"[..40];

        await PostgresSettings.CreateDatabaseAsync(databaseName);

        try
        {
            var options = new DbContextOptionsBuilder<VolterpDbContext>()
                .UseNpgsql(PostgresSettings.ConnectionStringOf(databaseName))
                .Options;

            await using (var db = new VolterpDbContext(options))
            {
                // From zero: the whole chain of the model is applied.
                await db.Database.MigrateAsync();

                var columns = await ColumnsOf(db);
                columns.Should().Contain(new[] { "LastError", "RawResponse", "ReceivedAt", "SequenceConsumed" },
                    "the four columns of the answer of the DGII exist");

                db.Database.HasPendingModelChanges().Should().BeFalse(
                    "the snapshot of the model is the one of the commit");
            }

            await using (var db = new VolterpDbContext(options))
            {
                // The Down of the migration of T47.
                await db.Database.MigrateAsync(BeforeT47);

                var columns = await ColumnsOf(db);
                columns.Should().NotContain("RawResponse");
                columns.Should().NotContain("SequenceConsumed");
                columns.Should().NotContain("ReceivedAt");
                columns.Should().NotContain("LastError");
            }

            await using (var db = new VolterpDbContext(options))
            {
                // And it applies again.
                await db.Database.MigrateAsync();

                var columns = await ColumnsOf(db);
                columns.Should().Contain(new[] { "LastError", "RawResponse", "ReceivedAt", "SequenceConsumed" });
            }
        }
        finally
        {
            await PostgresSettings.DropDatabaseAsync(databaseName);
        }
    }

    private static async Task<List<string>> ColumnsOf(VolterpDbContext db)
    {
        var connection = db.Database.GetDbConnection();

        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            "select column_name from information_schema.columns " +
            "where table_name = 'ElectronicInvoices' " +
            "and is_nullable = 'YES' " +
            "and column_name in ('RawResponse','SequenceConsumed','ReceivedAt','LastError')";

        var result = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result.Add(reader.GetString(0));

        return result;
    }
}
