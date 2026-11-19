using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Volterp.Domain.Entities;
using Volterp.Infrastructure.Data;
using Xunit;

namespace Volterp.Tests.Infrastructure;

// The chain of the migrations and the model: they have to agree, the downs
// of the e-CF migrations have to exist and the indexes of the document have
// to be unique. These are the probes of the verifier of the migrations.
//
// The chain is never applied here: the SQL of the seeds is Postgres and the
// suite runs on SQLite.
public class MigrationChainTests
{
    private const string ConnectionString =
        "Host=localhost;Database=volterp;Username=volterp;Password=temppassword123;Port=5432";

    // The migrations of the e-CF, with the task that wrote each one. Every
    // one of them has to be able to roll back: a Down that does nothing
    // leaves the schema of the client in the state of the migration.
    public static TheoryData<string> EcfMigrations => new()
    {
        "20261104163914_AddElectronicInvoices",
        "20261113120000_AddEcfRangesAndNoteReferences",
        "20261113160000_UniqueDocumentPerSale",
        "20261114011904_AddDurableElectronicInvoiceQueue",
        "20261114221020_AddDgiiResponses",
        "20261115173226_AddReceivedElectronicInvoices",
        "20261115211134_AddNormalFiscalDocuments",
        "20261117031551_AddContingencyReplacement",
        "20261117130000_AddApiCredentials",
        "20261117140000_AddRangeAnnulment",
        "20261117150000_AddIssuedSeeds",
        "20261118201231_AddCompanyIssuers",
        "20261119031728_AddCompanyIssuerUniqueRnc"
    };

    [Fact]
    public void TheModelHasNoPendingChangesAgainstTheLastMigration()
    {
        using var context = Context();

        var migrationsAssembly = context.GetService<IMigrationsAssembly>();
        var initializer = context.GetService<IModelRuntimeInitializer>();

        var snapshotModel = migrationsAssembly.ModelSnapshot?.Model;

        if (snapshotModel is not null)
            snapshotModel = initializer.Initialize(snapshotModel, true, null);

        var designTimeModel = context.GetService<IDesignTimeModel>().Model;

        if (migrationsAssembly.ModelSnapshot is IMutableModel mutableModel)
        {
            designTimeModel = initializer.Initialize(mutableModel.FinalizeModel(), true, null);
        }

        var differ = context.GetService<IMigrationsModelDiffer>();

        differ.HasDifferences(
                snapshotModel?.GetRelationalModel(),
                designTimeModel.GetRelationalModel())
            .Should().BeFalse(
                "the model and the migrations have to agree: a change in the model without a migration leaves the database of the client behind");
    }

    [Theory]
    [MemberData(nameof(EcfMigrations))]
    public void TheDownOfTheEcfMigrationIsNotEmpty(string migrationId)
    {
        using var context = Context();

        var migrations = context.GetService<IMigrationsAssembly>().Migrations;

        migrations.Should().ContainKey(migrationId);

        var migration = (Migration)Activator.CreateInstance(migrations[migrationId].AsType())!;

        DownOperationsOf(migration).Should().NotBeEmpty(
            "a rollback that does nothing leaves the schema of the client in the state of the migration");
    }

    [Fact]
    public void TheReceivedDocumentUsesTheGlobalNaturalKey()
    {
        using var context = Context();

        var entity = context.Model.FindEntityType(typeof(ReceivedElectronicInvoice));
        entity.Should().NotBeNull();

        var naturalKey = entity!.GetIndexes()
            .Single(index => index.Properties.Count == 2
                && index.Properties[0].Name == nameof(ReceivedElectronicInvoice.IssuerTaxId)
                && index.Properties[1].Name == nameof(ReceivedElectronicInvoice.ENCF));

        naturalKey.IsUnique.Should().BeTrue(
            "the same issuer and e-NCF cannot be received twice, even for a different tenant");
    }

    [Fact]
    public void TheIndexesOfTheDocumentAreUniqueInTheModel()
    {
        using var context = Context();

        var entity = context.Model.FindEntityType(typeof(ElectronicInvoice));

        entity.Should().NotBeNull();

        var encf = entity!.GetIndexes()
            .Single(index => index.Properties.Count == 2
                && index.Properties[0].Name == nameof(ElectronicInvoice.IssuerTaxId)
                && index.Properties[1].Name == nameof(ElectronicInvoice.ENCF));

        encf.IsUnique.Should().BeTrue(
            "the same e-NCF cannot exist twice for the same issuer: it would be two fiscal documents with one number");

        var sale = entity.GetIndexes()
            .Single(index => index.Properties.Count == 1
                && index.Properties[0].Name == nameof(ElectronicInvoice.SaleId));

        sale.IsUnique.Should().BeTrue(
            "a sale cannot have two fiscal documents: the second one would burn another number");
    }

    private static VolterpDbContext Context()
    {
        var options = new DbContextOptionsBuilder<VolterpDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new VolterpDbContext(options);
    }

    // The operations of the down are built the first time the migration runs.
    // Here they are asked for directly, so the test does not need a database.
    private static IReadOnlyList<MigrationOperation> DownOperationsOf(Migration migration)
    {
        var operations = migration.DownOperations;

        if (operations is { Count: > 0 })
            return operations;

        var builder = Builder();
        var down = migration.GetType().GetMethod("Down", BindingFlags.Instance | BindingFlags.NonPublic);

        down.Should().NotBeNull("a migration without a down is not a migration of this chain");

        down!.Invoke(migration, [builder]);

        return builder.Operations;
    }

    private static MigrationBuilder Builder()
    {
        var constructor = typeof(MigrationBuilder)
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .First(candidate => candidate.GetParameters().Length == 1);

        var activeProvider = Activator.CreateInstance(
            constructor.GetParameters()[0].ParameterType,
            "Npgsql.EntityFrameworkCore.PostgreSQL",
            "10.0.0");

        return (MigrationBuilder)constructor.Invoke([activeProvider]);
    }
}
