using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using EitherWay;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Volterp.Application.DTOs.SaleDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Interfaces;
using Volterp.Application.Services;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.ElectronicInvoicing;
using Volterp.ElectronicInvoicing.Abstractions;
using Volterp.ElectronicInvoicing.Dgii;
using Volterp.ElectronicInvoicing.Signing;
using Volterp.ElectronicInvoicing.Xml;
using Volterp.Infrastructure.Data;
using Volterp.Infrastructure.UnitOfWork;
using Xunit;

namespace Volterp.Tests.Integration;

// The taxpayer in contingency issues on paper and owes the DGII the
// replacement of every paper document: the e-CF of the replacement carries
// the reference of the document it replaces, it goes only to the DGII and it
// goes from the moment the contingency is lifted.
public class ContingencyReplacementTests
{
    [Fact]
    public async Task ASaleCompletedInsideTheContingency_LeavesThePaperDocumentAndTheReplacementWaiting()
    {
        await using var fixture = new ContingencyFixture();
        await fixture.InitializeAsync();

        var result = await fixture.CompleteSaleAsync();

        result.Should().BeOfType<Either<Error, SaleDto>.Right>();

        var paper = await fixture.ReadAsync(context => context.NormalFiscalDocuments
            .AsNoTracking()
            .SingleAsync());

        paper.Number.Should().Be("B0200000001");
        paper.DocumentType.Should().Be(NormalFiscalDocumentType.Consumer);
        paper.IssuedAt.Should().NotBe(default);

        var range = await fixture.ReadAsync(context => context.NormalFiscalSequences
            .AsNoTracking()
            .SingleAsync());

        range.CurrentSequence.Should().Be(1,
            "the paper document takes its number from the range of the normal documents");

        var invoice = await fixture.ReadAsync(context => context.ElectronicInvoices
            .AsNoTracking()
            .SingleAsync());

        invoice.ReplacesContingencyDocument.Should().BeTrue();
        invoice.Status.Should().Be(EcfStatus.Signed);
        invoice.SignedXml.Should().Contain("Signature");
        invoice.SendNotBefore.Should().Be(fixture.ContingencyEndsAt,
            "the replacement leaves for the DGII from the lifting of the contingency");

        fixture.Dgii.SendRequests.Should().Be(0);
    }

    [Fact]
    public async Task TheReplacement_CarriesTheReferenceOfThePaperDocument()
    {
        await using var fixture = new ContingencyFixture();
        await fixture.InitializeAsync();

        await fixture.CompleteSaleAsync();

        var paper = await fixture.ReadAsync(context => context.NormalFiscalDocuments
            .AsNoTracking()
            .SingleAsync());

        var invoice = await fixture.ReadAsync(context => context.ElectronicInvoices
            .AsNoTracking()
            .SingleAsync());

        // The norm: the e-CF of the replacement identifies the paper document
        // it replaces, with the code of the replacement.
        invoice.SignedXml.Should().Contain($"<NCFModificado>{paper.Number}</NCFModificado>");
        invoice.SignedXml.Should().Contain($"<FechaNCFModificado>{paper.IssuedAt:dd-MM-yyyy}</FechaNCFModificado>");
        invoice.SignedXml.Should().Contain("<CodigoModificacion>4</CodigoModificacion>");
    }

    [Fact]
    public async Task TheReplacement_WaitsForTheEndOfTheContingency_AndGoesToTheDgiiWhenItIsLifted()
    {
        await using var fixture = new ContingencyFixture();
        await fixture.InitializeAsync();

        await fixture.CompleteSaleAsync();

        var waiting = await fixture.Invoices.ProcessPendingAsync(fixture.CompanyId);

        waiting.Should().BeOfType<Either<Error, int>.Right>();
        fixture.Dgii.SendRequests.Should().Be(0,
            "the replacement does not go to the DGII while the contingency is open");

        fixture.Clock.Advance(TimeSpan.FromHours(2));

        var lifted = await fixture.Invoices.ProcessPendingAsync(fixture.CompanyId);

        lifted.Should().BeOfType<Either<Error, int>.Right>();
        fixture.Dgii.SendRequests.Should().Be(1,
            "the replacement goes to the DGII once the contingency is lifted");

        var invoice = await fixture.ReadAsync(context => context.ElectronicInvoices
            .AsNoTracking()
            .SingleAsync());

        invoice.Status.Should().Be(EcfStatus.Sent);
    }

    [Fact]
    public async Task TheReplacement_KeepsTheTypeOfThePaperDocumentItReplaces()
    {
        await using var fixture = new ContingencyFixture();
        await fixture.InitializeAsync();

        await fixture.CompleteSaleAsync();

        var paper = await fixture.ReadAsync(context => context.NormalFiscalDocuments
            .AsNoTracking()
            .SingleAsync());

        var invoice = await fixture.ReadAsync(context => context.ElectronicInvoices
            .AsNoTracking()
            .SingleAsync());

        // The norm: the type of the e-CF is the equivalent type of the paper
        // document it replaces.
        NormalFiscalDocumentTypes.For(invoice.DocumentType).Should().Be(paper.DocumentType);
        invoice.DocumentType.Should().Be(EcfDocumentType.Consumer);
        invoice.ENCF.Should().StartWith("E32");
    }

    [Fact]
    public async Task TheReplacement_GoesToTheDgiiWhenTheTaxpayerLiftsTheContingencyBeforeItsDay()
    {
        await using var fixture = new ContingencyFixture();
        await fixture.InitializeAsync(endsAfter: TimeSpan.FromDays(10));

        await fixture.CompleteSaleAsync();

        await fixture.LiftTheContingencyAsync();

        var result = await fixture.Invoices.ProcessPendingAsync(fixture.CompanyId);

        result.Should().BeOfType<Either<Error, int>.Right>();
        fixture.Dgii.SendRequests.Should().Be(1,
            "the replacement goes from the lifting of the contingency, not from the day it was waiting for");

        var invoice = await fixture.ReadAsync(context => context.ElectronicInvoices
            .AsNoTracking()
            .SingleAsync());

        invoice.Status.Should().Be(EcfStatus.Sent);
    }

    [Fact]
    public async Task AReplacementWhoseContingencyIsStillOpenAfterFifteenDays_GoesToReview()
    {
        await using var fixture = new ContingencyFixture();
        await fixture.InitializeAsync(
            startsBefore: TimeSpan.FromDays(16),
            endsAfter: TimeSpan.FromDays(20));

        await fixture.CompleteSaleAsync();

        fixture.Clock.Advance(TimeSpan.FromDays(16));

        var result = await fixture.Invoices.ProcessPendingAsync(fixture.CompanyId);

        result.Should().BeOfType<Either<Error, int>.Right>();
        fixture.Dgii.SendRequests.Should().Be(0);

        var invoice = await fixture.ReadAsync(context => context.ElectronicInvoices
            .AsNoTracking()
            .SingleAsync());

        // The norm does not allow the contingency to last more than fifteen
        // days: a replacement that waits longer than that needs a human.
        invoice.RequiresReview.Should().BeTrue();
        invoice.VerdictNote.Should().Contain("fifteen days");
    }

    [Fact]
    public async Task ACompanyInTheNormalMode_LeavesThePaperDocumentAndNoElectronicInvoice()
    {
        await using var fixture = new ContingencyFixture();
        await fixture.InitializeAsync(emissionMode: CompanyEmissionMode.Normal, inContingency: false);

        await fixture.CompleteSaleAsync();

        (await fixture.ReadAsync(context => context.NormalFiscalDocuments.CountAsync())).Should().Be(1);
        (await fixture.ReadAsync(context => context.ElectronicInvoices.CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task ACompanyInTheElectronicModeWithoutContingency_LeavesTheElectronicDocumentAndNoPaperDocument()
    {
        await using var fixture = new ContingencyFixture();
        await fixture.InitializeAsync(inContingency: false);

        await fixture.CompleteSaleAsync();

        var invoice = await fixture.ReadAsync(context => context.ElectronicInvoices
            .AsNoTracking()
            .SingleAsync());

        invoice.ReplacesContingencyDocument.Should().BeFalse();
        invoice.SendNotBefore.Should().BeNull();
        invoice.Status.Should().Be(EcfStatus.Signed);

        (await fixture.ReadAsync(context => context.NormalFiscalDocuments.CountAsync())).Should().Be(0);
    }

    private sealed class ContingencyFixture : IAsyncDisposable
    {
        private const string IssuerRnc = "10100000101";

        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<VolterpDbContext> _options;

        public ContingencyFixture()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            _options = new DbContextOptionsBuilder<VolterpDbContext>()
                .UseSqlite(_connection)
                .Options;

            Context = new VolterpDbContext(_options);
        }

        public VolterpDbContext Context { get; }
        public UnitOfWork UnitOfWork { get; private set; } = null!;
        public SaleService Sales { get; private set; } = null!;
        public IElectronicInvoiceService Invoices { get; private set; } = null!;
        public DgiiStub Dgii { get; } = new();
        public MovableClock Clock { get; } = new();
        public Guid CompanyId { get; private set; }
        public Guid SaleId { get; private set; }
        public DateTime ContingencyStartsAt { get; private set; }
        public DateTime ContingencyEndsAt { get; private set; }

        public async Task InitializeAsync(
            CompanyEmissionMode emissionMode = CompanyEmissionMode.Electronic,
            bool inContingency = true,
            TimeSpan? startsBefore = null,
            TimeSpan? endsAfter = null)
        {
            await Context.Database.EnsureCreatedAsync();

            // The clock is a minute ahead of the rows of the fixture so the
            // documents it writes are not born in the future.
            Clock.MoveTo(DateTimeOffset.UtcNow.AddMinutes(1));

            CompanyId = Guid.CreateVersion7();
            SaleId = Guid.CreateVersion7();

            ContingencyStartsAt = Clock.GetUtcNow().UtcDateTime - (startsBefore ?? TimeSpan.FromHours(1));
            ContingencyEndsAt = Clock.GetUtcNow().UtcDateTime + (endsAfter ?? TimeSpan.FromHours(1));

            Context.Companies.Add(Company(emissionMode, inContingency));
            Context.Sales.Add(Sale());

            // The range of the paper documents is a range of its own: the
            // electronic one never moves it.
            Context.NormalFiscalSequences.Add(new NormalFiscalSequence
            {
                Id = Guid.CreateVersion7(),
                CompanyId = CompanyId,
                IssuerTaxId = IssuerRnc,
                DocumentType = NormalFiscalDocumentType.Consumer,
                From = 1,
                To = 50_000_000,
                CurrentSequence = 0
            });

            Context.ElectronicInvoiceSequences.Add(new ElectronicInvoiceSequence
            {
                Id = Guid.CreateVersion7(),
                CompanyId = CompanyId,
                IssuerTaxId = IssuerRnc,
                DocumentType = EcfDocumentType.Consumer,
                From = 1,
                To = 50_000_000,
                CurrentSequence = 0
            });

            await Context.SaveChangesAsync();

            // The sale is read again by the services, the way a request reads
            // it: nothing stays tracked.
            Context.ChangeTracker.Clear();

            UnitOfWork = new UnitOfWork(Context);

            var certificates = new Mock<ISigningCertificateProvider>();
            certificates.Setup(c => c.GetSigningCertificateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateCertificate());

            var signer = new XmlDsigEcfSigner();
            var authenticator = new DgiiAuthenticator(Dgii, signer, new DgiiTokenCache());

            Invoices = new ElectronicInvoiceService(
                UnitOfWork,
                new EcfXmlWriter(),
                signer,
                certificates.Object,
                authenticator,
                Dgii,
                timeProvider: Clock);

            Sales = new SaleService(
                UnitOfWork,
                Invoices,
                new NormalFiscalDocumentService(UnitOfWork),
                Clock);
        }

        public Task<Either<Error, SaleDto>> CompleteSaleAsync()
            => Sales.CompleteSaleAsync(SaleId, CompanyId, Guid.CreateVersion7());

        // The taxpayer lifts the contingency: the declaration goes back to
        // none.
        public async Task LiftTheContingencyAsync()
        {
            var company = await Context.Companies.SingleAsync(c => c.Id == CompanyId);

            company.DeclareContingency(CompanyContingencyStatus.None, null, null);

            await Context.SaveChangesAsync();
            Context.ChangeTracker.Clear();
        }

        // The truth is in the database: the raw statements that take the
        // numbers do not go through the change tracker.
        public async Task<T> ReadAsync<T>(Func<VolterpDbContext, Task<T>> read)
        {
            await using var context = new VolterpDbContext(_options);
            return await read(context);
        }

        private Company Company(CompanyEmissionMode emissionMode, bool inContingency) => new()
        {
            Id = CompanyId,
            Name = "Distribuidora del Norte",
            LegalName = "Distribuidora del Norte SRL",
            TaxId = IssuerRnc,
            Address = "Calle Principal 1, Santo Domingo",
            Phone = "809-555-1234",
            Email = "facturacion@distribuidora.do",
            EmissionMode = emissionMode,
            ContingencyStatus = inContingency ? CompanyContingencyStatus.Total : CompanyContingencyStatus.None,
            ContingencyStartsAt = inContingency ? ContingencyStartsAt : null,
            ContingencyEndsAt = inContingency ? ContingencyEndsAt : null
        };

        private Sale Sale()
        {
            var sale = new Sale
            {
                Id = SaleId,
                CompanyId = CompanyId,
                Code = "V-0001",
                Status = SaleStatus.Pending,
                DocumentType = EcfDocumentType.Consumer,
                ReceptorTaxId = "101010101",
                ClienteName = "Cliente de Prueba SRL",
                CreatedAt = DateTime.UtcNow
            };

            sale.AddItem(Guid.CreateVersion7(), "Café molido", 2, 100m, TaxCode.ItbisTasa1);

            return sale;
        }

        private static X509Certificate2 CreateCertificate()
        {
            const string password = "volterp-test-password";

            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest(
                "CN=Volterp Test Emisor, C=DO",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            var certificate = request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddDays(365));

            return X509CertificateLoader.LoadPkcs12(
                certificate.Export(X509ContentType.Pfx, password),
                password,
                X509KeyStorageFlags.EphemeralKeySet);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    // The clock the tests move: the contingency opens and closes with it.
    private sealed class MovableClock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow() => _now;

        public void MoveTo(DateTimeOffset now) => _now = now;

        public void Advance(TimeSpan delta) => _now = _now.Add(delta);
    }

    // The DGII as the tests see it: it answers the seed, the token, the
    // acknowledgement and the verdict, and it counts what it was asked.
    private sealed class DgiiStub : IDgiiClient
    {
        public int SendRequests { get; private set; }
        public int StatusRequests { get; private set; }

        public Task<string> GetSeedAsync(CancellationToken cancellationToken = default)
            => Task.FromResult("<SemillaModel><valor>abc</valor><fecha>2019-03-13T14:33:32-04:00</fecha></SemillaModel>");

        public Task<DgiiToken> RequestTokenAsync(string signedSeedXml, CancellationToken cancellationToken = default)
            => Task.FromResult(new DgiiToken("jwt", DateTimeOffset.UtcNow.AddHours(1), DateTimeOffset.UtcNow));

        public Task<DgiiReceipt> SendElectronicInvoiceAsync(string signedEcfXml, string token, CancellationToken cancellationToken = default)
        {
            SendRequests++;

            return Task.FromResult(new DgiiReceipt("track-1", null, null));
        }

        public Task<DgiiStatus> GetStatusAsync(string trackId, string token, CancellationToken cancellationToken = default)
        {
            StatusRequests++;

            return Task.FromResult(new DgiiStatus(
                trackId,
                DgiiVerdict.Accepted,
                "Aceptado",
                "10100000101",
                "E320000000001",
                true,
                DateTimeOffset.UtcNow,
                []));
        }

        public Task<DgiiTrackLookup> GetTrackIdAsync(string issuerTaxId, string encf, string token, CancellationToken cancellationToken = default)
            => Task.FromResult(new DgiiTrackLookup("track-1", "Aceptado", DateTimeOffset.UtcNow));
    }
}
