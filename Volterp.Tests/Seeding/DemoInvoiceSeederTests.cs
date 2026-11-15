using EitherWay;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Volterp.Application.Abstractions;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.ElectronicInvoicing;
using Volterp.Infrastructure.Data;
using Volterp.Infrastructure.UnitOfWork;
using Volterp.Tools.Seeding;
using Xunit;

namespace Volterp.Tests.Seeding;

public class DemoInvoiceSeederTests
{
    private const string TargetTaxId = "101500018";
    private const string OtherTaxId = "101500026";
    private const string UnknownTaxId = "101500093";

    [Fact]
    public async Task RunningItTwiceKeepsTheSameFourInvoices()
    {
        await using var fixture = new DemoInvoiceFixture();
        await fixture.InitializeAsync();

        var first = await SeedAsync(fixture);
        var invoicesAfterFirstRun = await fixture.InvoiceEncfsAsync(fixture.TargetCompanyId);

        var second = await SeedAsync(fixture);
        var invoicesAfterSecondRun = await fixture.InvoiceEncfsAsync(fixture.TargetCompanyId);

        first.CreatedCount.Should().Be(4);
        first.ExistingCount.Should().Be(0);
        second.CreatedCount.Should().Be(0);
        second.ExistingCount.Should().Be(4);
        invoicesAfterSecondRun.Should().Equal(invoicesAfterFirstRun);
        invoicesAfterSecondRun.Should().HaveCount(4);
        (await fixture.CountSalesAsync(fixture.TargetCompanyId))
            .Should().Be(4, "the second run must not create another sale either");
    }

    [Fact]
    public async Task RunningItTwiceUnderTheContextOfTheTenantKeepsTheSameFourInvoices()
    {
        var ambient = new ScopedTenantContext();
        await using var fixture = new DemoInvoiceFixture(ambient);
        ambient.CompanyId = fixture.TargetCompanyId;
        await fixture.InitializeAsync();

        var first = await SeedAsync(fixture);
        var second = await SeedAsync(fixture);

        first.CreatedCount.Should().Be(4);
        second.CreatedCount.Should().Be(0);
        second.ExistingCount.Should().Be(4);
        (await fixture.CountInvoicesAsync(fixture.TargetCompanyId)).Should().Be(4);
        (await fixture.CountSalesAsync(fixture.TargetCompanyId)).Should().Be(4);
    }

    [Fact]
    public async Task RunningItTwiceWithoutACompanyInTheContextKeepsTheSameFourInvoices()
    {
        var ambient = new ScopedTenantContext();
        await using var fixture = new DemoInvoiceFixture(ambient);
        await fixture.InitializeAsync();

        var first = await SeedAsync(fixture);
        var second = await SeedAsync(fixture);

        first.CreatedCount.Should().Be(4);
        second.CreatedCount.Should().Be(0,
            "the invoices of the tenant are found even when the context carries no company");
        second.ExistingCount.Should().Be(4);
        (await fixture.CountInvoicesAsync(fixture.TargetCompanyId)).Should().Be(4);
        (await fixture.CountSalesAsync(fixture.TargetCompanyId)).Should().Be(4);
    }

    [Fact]
    public async Task ItSeedsTheTenantEvenWhenTheContextCarriesAnotherTenant()
    {
        var ambient = new ScopedTenantContext();
        await using var fixture = new DemoInvoiceFixture(ambient);
        ambient.CompanyId = fixture.OtherCompanyId;
        await fixture.InitializeAsync();

        var other = await fixture.AddOtherTenantInvoiceAsync();

        var first = await SeedAsync(fixture);
        var second = await SeedAsync(fixture);

        first.CreatedCount.Should().Be(4);
        second.CreatedCount.Should().Be(0);
        second.ExistingCount.Should().Be(4);
        second.PendingEmissionCount.Should().Be(1);
        second.InProcessCount.Should().Be(1);
        second.RejectedCount.Should().Be(1);
        second.RequiresReviewCount.Should().Be(1);

        (await fixture.CountInvoicesAsync(fixture.TargetCompanyId)).Should().Be(4);
        (await fixture.CountInvoicesAsync(fixture.OtherCompanyId)).Should().Be(1);
        (await fixture.CountSalesAsync(fixture.OtherCompanyId)).Should().Be(1);

        var untouched = await fixture.InvoiceAsync(other.Id);
        untouched.CompanyId.Should().Be(fixture.OtherCompanyId);
        untouched.ENCF.Should().Be(other.ENCF);
        untouched.Status.Should().Be(other.Status);
        untouched.RequiresReview.Should().Be(other.RequiresReview);
    }

    [Fact]
    public async Task ItDoesNotTouchAnotherTenant()
    {
        await using var fixture = new DemoInvoiceFixture();
        await fixture.InitializeAsync();

        var other = await fixture.AddOtherTenantInvoiceAsync();

        await SeedAsync(fixture);

        var untouched = await fixture.InvoiceAsync(other.Id);

        untouched.CompanyId.Should().Be(fixture.OtherCompanyId);
        untouched.ENCF.Should().Be(other.ENCF);
        untouched.Status.Should().Be(other.Status);
        (await fixture.CountInvoicesAsync(fixture.OtherCompanyId)).Should().Be(1);
        (await fixture.CountSalesAsync(fixture.OtherCompanyId)).Should().Be(1);
        (await fixture.CountInvoicesAsync(fixture.TargetCompanyId)).Should().Be(4);
    }

    [Fact]
    public async Task ItWorksWithoutACertificateAndDoesNotSign()
    {
        await using var fixture = new DemoInvoiceFixture();
        await fixture.InitializeAsync();

        var outcome = await SeedAsync(fixture);

        outcome.CreatedCount.Should().Be(4);
        (await fixture.Context.CompanyCertificates.CountAsync()).Should().Be(0);

        var invoices = await fixture.InvoicesAsync(fixture.TargetCompanyId);

        invoices.Should().HaveCount(4);
        invoices.Should().OnlyContain(i =>
            string.IsNullOrWhiteSpace(i.SignedXml)
            && i.SignedAt == null
            && i.OriginalXml == null
            && i.SecurityCode == null
            && i.QrUrl == null);

        var pending = invoices.Single(i => i.Status == EcfStatus.Signed);
        pending.NextAttemptAt.Should().BeAfter(DateTime.UtcNow,
            "the demo invoice must not enter the queue that talks to the DGII");

        var inProcess = invoices.Single(i => i.Status == EcfStatus.InProcess);
        inProcess.NextCheckAt.Should().BeAfter(DateTime.UtcNow,
            "the demo invoice must not enter the queue that asks the DGII for a verdict");

        var rejected = invoices.Single(i => i.Status == EcfStatus.Rejected);
        rejected.VerdictNote.Should().NotBeNullOrWhiteSpace();

        var review = invoices.Single(i => i.RequiresReview);
        review.Status.Should().Be(EcfStatus.Draft);
        review.VerdictNote.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ARefusedRunLeavesTheStoredDataUnchanged()
    {
        await using var fixture = new DemoInvoiceFixture();
        await fixture.InitializeAsync();

        var invalid = await new DemoInvoiceSeeder(new UnitOfWork(fixture.Context), fixture.Context)
            .SeedAsync("123");

        var unknown = await new DemoInvoiceSeeder(new UnitOfWork(fixture.Context), fixture.Context)
            .SeedAsync(UnknownTaxId);

        invalid.Should().BeOfType<Either<Error, DemoInvoiceSeedOutcome>.Left>();
        unknown.Should().BeOfType<Either<Error, DemoInvoiceSeedOutcome>.Left>();
        (await fixture.CountInvoicesAsync(fixture.TargetCompanyId)).Should().Be(0);
        (await fixture.CountSalesAsync(fixture.TargetCompanyId)).Should().Be(0);
    }

    private static async Task<DemoInvoiceSeedOutcome> SeedAsync(DemoInvoiceFixture fixture)
    {
        var result = await new DemoInvoiceSeeder(new UnitOfWork(fixture.Context), fixture.Context)
            .SeedAsync(TargetTaxId);

        result.Should().BeOfType<Either<Error, DemoInvoiceSeedOutcome>.Right>(
            "the demo seeder was expected to succeed");

        return ((Either<Error, DemoInvoiceSeedOutcome>.Right)result).Value;
    }

    private sealed class ScopedTenantContext : ITenantContext
    {
        public Guid? CompanyId { get; set; }

        public bool IsUnrestricted => false;
    }

    private sealed class DemoInvoiceFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        public DemoInvoiceFixture(ITenantContext? tenantContext = null)
        {
            TargetCompanyId = Guid.CreateVersion7();
            OtherCompanyId = Guid.CreateVersion7();

            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<VolterpDbContext>()
                .UseSqlite(_connection)
                .Options;

            Context = new VolterpDbContext(options, tenantContext);
        }

        public VolterpDbContext Context { get; }
        public Guid TargetCompanyId { get; }
        public Guid OtherCompanyId { get; }

        public async Task InitializeAsync()
        {
            await Context.Database.EnsureCreatedAsync();

            Context.Companies.AddRange(
                Company(TargetCompanyId, TargetTaxId, "Distribuidora del Caribe"),
                Company(OtherCompanyId, OtherTaxId, "Farmacias Vida Nueva"));

            await Context.SaveChangesAsync();
        }

        public async Task<ElectronicInvoice> AddOtherTenantInvoiceAsync()
        {
            var sale = new Sale
            {
                Id = Guid.CreateVersion7(),
                CompanyId = OtherCompanyId,
                Code = "OTHER-SALE",
                Status = SaleStatus.Completed,
                DocumentType = EcfDocumentType.Consumer,
                Subtotal = 200m,
                Itbis = 36m,
                Total = 236m
            };

            var invoice = new ElectronicInvoice
            {
                Id = Guid.CreateVersion7(),
                CompanyId = OtherCompanyId,
                SaleId = sale.Id,
                ENCF = "E320000009001",
                DocumentType = EcfDocumentType.Consumer,
                IssuerTaxId = OtherTaxId,
                SignedXml = "<ECF />",
                Status = EcfStatus.Signed
            };

            Context.Sales.Add(sale);
            Context.ElectronicInvoices.Add(invoice);
            await Context.SaveChangesAsync();

            return invoice;
        }

        public Task<int> CountInvoicesAsync(Guid companyId)
            => Context.ElectronicInvoices
                .IgnoreQueryFilters()
                .CountAsync(i => i.CompanyId == companyId);

        public Task<int> CountSalesAsync(Guid companyId)
            => Context.Sales
                .IgnoreQueryFilters()
                .CountAsync(s => s.CompanyId == companyId);

        public Task<List<string>> InvoiceEncfsAsync(Guid companyId)
            => Context.ElectronicInvoices
                .IgnoreQueryFilters()
                .Where(i => i.CompanyId == companyId)
                .OrderBy(i => i.ENCF)
                .Select(i => i.ENCF)
                .ToListAsync();

        public Task<List<ElectronicInvoice>> InvoicesAsync(Guid companyId)
            => Context.ElectronicInvoices
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(i => i.CompanyId == companyId)
                .ToListAsync();

        public Task<ElectronicInvoice> InvoiceAsync(Guid id)
            => Context.ElectronicInvoices
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleAsync(i => i.Id == id);

        private static Company Company(Guid id, string taxId, string name)
            => new()
            {
                Id = id,
                Name = name,
                TaxId = taxId,
                Address = "Address",
                LegalName = name,
                Phone = "8090000000",
                Email = "demo@volterp.test"
            };

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
