using EitherWay;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Volterp.Application.DTOs.ElectronicInvoiceDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Services;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.ElectronicInvoicing;
using Volterp.ElectronicInvoicing.Abstractions;
using Volterp.ElectronicInvoicing.Dgii;
using Volterp.ElectronicInvoicing.Xml;
using Volterp.Infrastructure.Data;
using Volterp.Infrastructure.UnitOfWork;
using Xunit;

namespace Volterp.Tests.Integration;

// The probes of the fiscal surface: the limits of the list, the taxonomy of
// the open documents and the order of the answer. They are the contract the
// front depends on, so they are pinned from the service and not from the
// code that produces them.
public class ElectronicInvoiceStateServiceProbeTests
{
    private const string LimitMessage = "limit must be greater than zero.";
    private const string CompanyMessage = "companyId must not be empty.";

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public async Task ALimitThatIsNotPositive_ReturnsLeftWithItsMessage(int limit)
    {
        await using var fixture = new StateFixture();
        await fixture.InitializeAsync();

        var result = await Service(fixture.Context).GetStateAsync(fixture.CompanyId, limit);

        result.Should().BeOfType<Either<Error, ElectronicInvoiceStateSurfaceDto>.Left>();
        ((Either<Error, ElectronicInvoiceStateSurfaceDto>.Left)result).Value.Message
            .Should().Be(LimitMessage);
    }

    [Fact]
    public async Task ALimitOfIntMaxValue_ReturnsEveryOpenDocumentWithItsCounts()
    {
        await using var fixture = new StateFixture();
        await fixture.InitializeAsync();

        await fixture.AddDocumentAsync(fixture.CompanyId, EcfStatus.Signed);
        await fixture.AddDocumentAsync(fixture.CompanyId, EcfStatus.InProcess);
        await fixture.AddDocumentAsync(fixture.CompanyId, EcfStatus.Rejected);
        await fixture.AddDocumentAsync(fixture.CompanyId, EcfStatus.Draft, requiresReview: true);

        var surface = await SurfaceOf(fixture, fixture.CompanyId, int.MaxValue);

        surface.Counts.Total.Should().Be(4);
        surface.Counts.PendingEmission.Should().Be(1);
        surface.Counts.InProcess.Should().Be(1);
        surface.Counts.Rejected.Should().Be(1);
        surface.Counts.RequiresReview.Should().Be(1);
        surface.Invoices.Should().HaveCount(4);
    }

    [Fact]
    public async Task ACompanyWithoutOpenDocuments_ReturnsZerosAndAnEmptyList()
    {
        await using var fixture = new StateFixture();
        await fixture.InitializeAsync();

        await fixture.AddDocumentAsync(fixture.OtherCompanyId, EcfStatus.Signed);

        var surface = await SurfaceOf(fixture, fixture.CompanyId, 50);

        surface.Counts.Total.Should().Be(0);
        surface.Counts.PendingEmission.Should().Be(0);
        surface.Counts.InProcess.Should().Be(0);
        surface.Counts.Rejected.Should().Be(0);
        surface.Counts.RequiresReview.Should().Be(0);
        surface.Invoices.Should().BeEmpty();
    }

    [Fact]
    public async Task AnAcceptedDocumentThatRequiresReview_NeitherCountsNorAppears()
    {
        await using var fixture = new StateFixture();
        await fixture.InitializeAsync();

        // The verdict of the DGII closes the document: the review flag does
        // not reopen what the DGII already answered.
        await fixture.AddDocumentAsync(
            fixture.CompanyId,
            EcfStatus.Accepted,
            requiresReview: true);

        var surface = await SurfaceOf(fixture, fixture.CompanyId, 50);

        surface.Counts.Total.Should().Be(0);
        surface.Counts.RequiresReview.Should().Be(0);
        surface.Invoices.Should().BeEmpty();
    }

    [Fact]
    public async Task ARejectedDocumentThatRequiresReview_AppearsAsRejected()
    {
        await using var fixture = new StateFixture();
        await fixture.InitializeAsync();

        await fixture.AddDocumentAsync(
            fixture.CompanyId,
            EcfStatus.Rejected,
            requiresReview: true,
            verdictNote: "El RNC del receptor no existe.");

        var surface = await SurfaceOf(fixture, fixture.CompanyId, 50);

        surface.Counts.Rejected.Should().Be(1);
        surface.Counts.RequiresReview.Should().Be(0, "the nullity of the document dominates the review flag");
        surface.Invoices.Should().ContainSingle();
        surface.Invoices[0].FiscalState.Should().Be(EcfFiscalState.Rejected);
    }

    [Fact]
    public async Task ABacklogOfDocuments_ReturnsTheWholeCountAndAListOfFifty()
    {
        await using var fixture = new StateFixture();
        await fixture.InitializeAsync();

        await fixture.AddDocumentsAsync(fixture.CompanyId, 120);

        var surface = await SurfaceOf(fixture, fixture.CompanyId, 50);

        surface.Counts.Total.Should().Be(120, "the count covers every open document, not only the listed ones");
        surface.Counts.PendingEmission.Should().Be(120);
        surface.Invoices.Should().HaveCount(50, "the list of the surface is capped");
    }

    [Fact]
    public async Task TheListBringsTheMostRecentDocumentsFirst()
    {
        await using var fixture = new StateFixture();
        await fixture.InitializeAsync();

        // The e-NCF carries the position of the number, so the order of the
        // list says which document is the newest one.
        var oldest = new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);
        var first = await fixture.AddDocumentAsync(fixture.CompanyId, EcfStatus.Signed, createdAt: oldest);
        var second = await fixture.AddDocumentAsync(fixture.CompanyId, EcfStatus.Signed, createdAt: oldest.AddDays(1));
        var third = await fixture.AddDocumentAsync(fixture.CompanyId, EcfStatus.Signed, createdAt: oldest.AddDays(2));

        var surface = await SurfaceOf(fixture, fixture.CompanyId, 50);

        surface.Invoices.Select(i => i.ENCF)
            .Should().Equal(
                new[] { third, second, first },
                "the front shows the newest documents first");
    }

    [Fact]
    public async Task ACompanyThatIsNotThere_ReturnsLeft()
    {
        await using var fixture = new StateFixture();
        await fixture.InitializeAsync();

        var result = await Service(fixture.Context).GetStateAsync(Guid.Empty, 50);

        result.Should().BeOfType<Either<Error, ElectronicInvoiceStateSurfaceDto>.Left>();
        ((Either<Error, ElectronicInvoiceStateSurfaceDto>.Left)result).Value.Message
            .Should().Be(CompanyMessage);
    }

    private static async Task<ElectronicInvoiceStateSurfaceDto> SurfaceOf(
        StateFixture fixture, Guid companyId, int limit)
    {
        var result = await Service(fixture.Context).GetStateAsync(companyId, limit);

        result.Should().BeOfType<Either<Error, ElectronicInvoiceStateSurfaceDto>.Right>();
        return ((Either<Error, ElectronicInvoiceStateSurfaceDto>.Right)result).Value;
    }

    private static ElectronicInvoiceService Service(VolterpDbContext context)
    {
        var client = new Mock<IDgiiClient>();
        var signer = new Mock<IEcfSigner>();

        return new ElectronicInvoiceService(
            new UnitOfWork(context),
            new EcfXmlWriter(),
            signer.Object,
            Mock.Of<ISigningCertificateProvider>(),
            new DgiiAuthenticator(client.Object, signer.Object, new DgiiTokenCache()),
            client.Object);
    }

    private sealed class StateFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private int _sequence;

        public StateFixture()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            Options = new DbContextOptionsBuilder<VolterpDbContext>()
                .UseSqlite(_connection)
                .Options;

            Context = new VolterpDbContext(Options);
        }

        public DbContextOptions<VolterpDbContext> Options { get; }
        public VolterpDbContext Context { get; }
        public Guid CompanyId { get; } = Guid.CreateVersion7();
        public Guid OtherCompanyId { get; } = Guid.CreateVersion7();

        public async Task InitializeAsync()
        {
            await Context.Database.EnsureCreatedAsync();

            Context.Companies.AddRange(
                Company(CompanyId, "101500018"),
                Company(OtherCompanyId, "101500019"));

            await Context.SaveChangesAsync();
        }

        public async Task<string> AddDocumentAsync(
            Guid companyId,
            EcfStatus status,
            bool requiresReview = false,
            string? verdictNote = null,
            DateTime? createdAt = null)
        {
            var document = Document(companyId, status, requiresReview, verdictNote, createdAt);
            Context.ElectronicInvoices.Add(document);
            await Context.SaveChangesAsync();

            return document.ENCF;
        }

        public async Task AddDocumentsAsync(Guid companyId, int count)
        {
            for (var index = 0; index < count; index++)
                Context.ElectronicInvoices.Add(Document(companyId, EcfStatus.Signed, false, null, null));

            await Context.SaveChangesAsync();
        }

        private ElectronicInvoice Document(
            Guid companyId,
            EcfStatus status,
            bool requiresReview,
            string? verdictNote,
            DateTime? createdAt)
        {
            var encf = $"E{(int)EcfDocumentType.Consumer:D2}{++_sequence:D10}";
            var saleId = Guid.CreateVersion7();

            Context.Sales.Add(new Sale
            {
                Id = saleId,
                CompanyId = companyId,
                Code = $"V-{encf}",
                Status = SaleStatus.Completed,
                DocumentType = EcfDocumentType.Consumer,
                Subtotal = 100m,
                Itbis = 18m,
                Total = 118m
            });

            return new ElectronicInvoice
            {
                Id = Guid.CreateVersion7(),
                CompanyId = companyId,
                SaleId = saleId,
                ENCF = encf,
                DocumentType = EcfDocumentType.Consumer,
                IssuerTaxId = companyId == CompanyId ? "101500018" : "101500019",
                SignedXml = "<ECF />",
                Status = status,
                VerdictNote = verdictNote,
                RequiresReview = requiresReview,
                CreatedAt = createdAt ?? DateTime.UtcNow
            };
        }

        private static Company Company(Guid id, string taxId)
            => new()
            {
                Id = id,
                Name = "Empresa",
                TaxId = taxId,
                Address = "Dirección",
                LegalName = "Empresa SRL",
                Phone = "8090000000",
                Email = "empresa@test.com"
            };

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
