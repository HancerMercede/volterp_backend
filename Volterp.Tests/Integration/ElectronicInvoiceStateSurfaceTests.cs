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

// The surface of the fiscal state of a company: the count by state and the
// open documents. The count and the list come from the same classification,
// so they cannot disagree about a document.
public class ElectronicInvoiceStateSurfaceTests
{
    private const string RejectionReason = "El RNC del receptor no existe.";
    private const string ReviewObservation = "El certificado del emisor expiró.";

    [Fact]
    public async Task TheCountAndTheListCannotDisagree()
    {
        await using var fixture = new StateFixture();
        await fixture.InitializeAsync();

        var surface = await SurfaceOf(fixture, fixture.CompanyId, 50);

        // Every document of the list is counted in its own state, and the
        // count of every state is the number of documents of that state.
        surface.Counts.PendingEmission.Should().Be(
            surface.Invoices.Count(i => i.FiscalState == EcfFiscalState.PendingEmission));
        surface.Counts.InProcess.Should().Be(
            surface.Invoices.Count(i => i.FiscalState == EcfFiscalState.InProcess));
        surface.Counts.Rejected.Should().Be(
            surface.Invoices.Count(i => i.FiscalState == EcfFiscalState.Rejected));
        surface.Counts.RequiresReview.Should().Be(
            surface.Invoices.Count(i => i.FiscalState == EcfFiscalState.RequiresReview));

        surface.Counts.Total.Should().Be(surface.Invoices.Count);
        surface.Counts.Total.Should().Be(7, "seven documents of the company are still open");
    }

    [Fact]
    public async Task TheCountCoversEveryOpenDocument_EvenWhenTheListIsLimited()
    {
        await using var fixture = new StateFixture();
        await fixture.InitializeAsync();

        var surface = await SurfaceOf(fixture, fixture.CompanyId, 2);

        surface.Invoices.Should().HaveCount(2);
        surface.Counts.Total.Should().Be(7, "the count covers all the open documents, not only the listed ones");
    }

    [Fact]
    public async Task TheDocumentOfAnotherCompanyNeitherCountsNorAppears()
    {
        await using var fixture = new StateFixture();
        await fixture.InitializeAsync();

        var surface = await SurfaceOf(fixture, fixture.CompanyId, 50);

        surface.Invoices.Should().NotContain(i => i.ENCF == fixture.OtherCompanyEncf);
        surface.Counts.Total.Should().Be(7, "the document of the other company does not count");
    }

    [Fact]
    public async Task TheListCarriesTheSaleOfEveryDocument()
    {
        await using var fixture = new StateFixture();
        await fixture.InitializeAsync();

        var surface = await SurfaceOf(fixture, fixture.CompanyId, 50);

        // The front joins the fiscal state with the sale on the screen.
        surface.Invoices.Should().OnlyContain(i => i.SaleId != Guid.Empty);
        surface.Invoices.Should().Contain(i => i.SaleId == fixture.SaleOfThePendingDocument);
    }

    [Fact]
    public async Task TheRejectedDocumentCarriesItsReason_AndTheOneThatRequiresReviewItsObservation()
    {
        await using var fixture = new StateFixture();
        await fixture.InitializeAsync();

        var surface = await SurfaceOf(fixture, fixture.CompanyId, 50);

        var rejected = surface.Invoices.Single(i => i.FiscalState == EcfFiscalState.Rejected);
        rejected.VerdictNote.Should().Be(RejectionReason, "the tenant has to know why the DGII refused the document");

        var review = surface.Invoices.Single(i => i.RequiresReview);
        review.VerdictNote.Should().Be(ReviewObservation, "the tenant has to know what stopped the document");
        review.RequiresReview.Should().BeTrue();
    }

    [Fact]
    public async Task TheClosedDocumentsDoNotAppear()
    {
        await using var fixture = new StateFixture();
        await fixture.InitializeAsync();

        var surface = await SurfaceOf(fixture, fixture.CompanyId, 50);

        surface.Invoices.Should().NotContain(i => i.ENCF == fixture.AcceptedEncf);
        surface.Invoices.Should().NotContain(i => i.ENCF == fixture.ConditionallyAcceptedEncf);
        surface.Invoices.Should().NotContain(i => i.ENCF == fixture.AnnulledEncf);
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
        public Guid CompanyId { get; private set; }
        public Guid OtherCompanyId { get; private set; }
        public Guid SaleOfThePendingDocument { get; private set; }
        public string OtherCompanyEncf { get; private set; } = string.Empty;
        public string AcceptedEncf { get; private set; } = string.Empty;
        public string ConditionallyAcceptedEncf { get; private set; } = string.Empty;
        public string AnnulledEncf { get; private set; } = string.Empty;

        public async Task InitializeAsync()
        {
            await Context.Database.EnsureCreatedAsync();

            CompanyId = Guid.CreateVersion7();
            OtherCompanyId = Guid.CreateVersion7();

            Context.Companies.AddRange(
                Company(CompanyId, "101500018"),
                Company(OtherCompanyId, "101500019"));

            SaleOfThePendingDocument = Guid.CreateVersion7();

            Context.ElectronicInvoices.AddRange(
                Document(CompanyId, EcfStatus.Signed, "E310000000001", SaleOfThePendingDocument),
                Document(CompanyId, EcfStatus.Signed, "E310000000002"),
                Document(CompanyId, EcfStatus.Sent, "E310000000003"),
                Document(CompanyId, EcfStatus.InProcess, "E310000000004"),
                Document(CompanyId, EcfStatus.Rejected, "E310000000005", verdictNote: RejectionReason),
                Document(CompanyId, EcfStatus.Draft, "E310000000006", verdictNote: ReviewObservation, requiresReview: true),
                // A draft nobody has to look at should not exist: it fails
                // closed into the review.
                Document(CompanyId, EcfStatus.Draft, "E310000000007"));

            AcceptedEncf = "E310000000008";
            ConditionallyAcceptedEncf = "E310000000009";
            AnnulledEncf = "E310000000010";
            OtherCompanyEncf = "E310000000011";

            Context.ElectronicInvoices.AddRange(
                Document(CompanyId, EcfStatus.Accepted, AcceptedEncf),
                Document(CompanyId, EcfStatus.AcceptedConditionally, ConditionallyAcceptedEncf),
                Document(CompanyId, EcfStatus.Annulled, AnnulledEncf),
                Document(OtherCompanyId, EcfStatus.Signed, OtherCompanyEncf));

            await Context.SaveChangesAsync();
        }

        private ElectronicInvoice Document(
            Guid companyId,
            EcfStatus status,
            string encf,
            Guid? saleId = null,
            string? verdictNote = null,
            bool requiresReview = false)
        {
            var sale = saleId ?? Guid.CreateVersion7();

            Context.Sales.Add(new Sale
            {
                Id = sale,
                CompanyId = companyId,
                Code = $"V-{encf}",
                Status = SaleStatus.Completed,
                DocumentType = EcfDocumentType.FiscalCredit,
                ReceptorTaxId = "210000001",
                Subtotal = 100m,
                Itbis = 18m,
                Total = 118m
            });

            return new ElectronicInvoice
            {
                Id = Guid.CreateVersion7(),
                CompanyId = companyId,
                SaleId = sale,
                ENCF = encf,
                DocumentType = EcfDocumentType.FiscalCredit,
                IssuerTaxId = "101500018",
                ReceiverTaxId = "210000001",
                Subtotal = 100m,
                Itbis = 18m,
                Total = 118m,
                SignedXml = "<ECF />",
                Status = status,
                VerdictNote = verdictNote,
                RequiresReview = requiresReview
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
