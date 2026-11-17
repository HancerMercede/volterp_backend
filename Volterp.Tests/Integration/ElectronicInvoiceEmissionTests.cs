using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using EitherWay;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Volterp.Application.DTOs.ElectronicInvoiceDtos;
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

// The emission over the real stack: the repositories, the atomic sequence,
// the writer and the signer. The document is left signed and waiting: the
// queue is the one that talks to the DGII.
public class ElectronicInvoiceEmissionTests
{
    [Fact]
    public async Task IssueAsync_PersistsTheDocumentSignedAndWaitingForTheQueue()
    {
        await using var fixture = new EmissionFixture();
        await fixture.InitializeAsync();

        var result = await fixture.Service.IssueAsync(fixture.SaleId, fixture.CompanyId);

        result.Should().BeOfType<Either<Error, ElectronicInvoiceDto>.Right>();
        var dto = ((Either<Error, ElectronicInvoiceDto>.Right)result).Value;
        dto.ENCF.Should().Be("E320000000001");
        dto.Status.Should().Be(EcfStatus.Signed);

        var invoice = await fixture.Context.ElectronicInvoices.SingleAsync();

        invoice.ENCF.Should().Be("E320000000001");
        invoice.CompanyId.Should().Be(fixture.CompanyId);
        invoice.SaleId.Should().Be(fixture.SaleId);
        invoice.DocumentType.Should().Be(EcfDocumentType.Consumer);
        invoice.IssuerTaxId.Should().Be("10100000101");
        invoice.Status.Should().Be(EcfStatus.Signed);
        invoice.TrackId.Should().BeNull();
        invoice.SignedXml.Should().Contain("Signature");
        invoice.SignedAt.Should().NotBeNull();
        invoice.SentAt.Should().BeNull();
        invoice.Subtotal.Should().Be(200m);
        invoice.Itbis.Should().Be(36m);
        invoice.Total.Should().Be(236m);

        // The queue is the one that sends the document to the DGII.
        fixture.Dgii.SendRequests.Should().Be(0);
    }

    [Fact]
    public async Task RetryAsync_AfterTheCertificateFails_SignsTheSameDocumentWithoutMovingTheSequence()
    {
        await using var fixture = new EmissionFixture();
        await fixture.InitializeAsync();
        fixture.CertificateProvider
            .Setup(c => c.GetSigningCertificateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("The signing certificate is unavailable."));

        var issue = await fixture.Service.IssueAsync(fixture.SaleId, fixture.CompanyId);

        issue.Should().BeOfType<Either<Error, ElectronicInvoiceDto>.Left>();
        fixture.Context.ChangeTracker.Clear();
        var invoice = await fixture.Context.ElectronicInvoices.SingleAsync();
        invoice.ENCF.Should().Be("E320000000001");
        invoice.Status.Should().Be(EcfStatus.Draft);
        invoice.RequiresReview.Should().BeTrue();
        invoice.VerdictNote.Should().Contain("signing certificate is unavailable");

        var range = await fixture.Context.ElectronicInvoiceSequences
            .SingleAsync(s => s.CompanyId == fixture.CompanyId && s.DocumentType == EcfDocumentType.Consumer);
        range.CurrentSequence.Should().Be(1);

        fixture.AllowSigning();
        var retry = await fixture.Service.RetryAsync(invoice.Id, fixture.CompanyId);

        retry.Should().BeOfType<Either<Error, ElectronicInvoiceDto>.Right>();
        var dto = ((Either<Error, ElectronicInvoiceDto>.Right)retry).Value;
        dto.ENCF.Should().Be("E320000000001");
        dto.Status.Should().Be(EcfStatus.Signed);
        dto.RequiresReview.Should().BeFalse();

        fixture.Context.ChangeTracker.Clear();
        var retried = await fixture.Context.ElectronicInvoices.SingleAsync();
        retried.ENCF.Should().Be("E320000000001");
        retried.Status.Should().Be(EcfStatus.Signed);
        retried.RequiresReview.Should().BeFalse();
        retried.SignedAt.Should().NotBeNull();

        var unchangedRange = await fixture.Context.ElectronicInvoiceSequences
            .SingleAsync(s => s.CompanyId == fixture.CompanyId && s.DocumentType == EcfDocumentType.Consumer);
        unchangedRange.CurrentSequence.Should().Be(1);
        fixture.Dgii.SendRequests.Should().Be(0);
    }

    [Fact]
    public async Task ProcessPendingAsync_ThenRetrackAsync_AcceptsTheDocumentSentByTheQueue()
    {
        await using var fixture = new EmissionFixture();
        await fixture.InitializeAsync();

        await fixture.Service.IssueAsync(fixture.SaleId, fixture.CompanyId);
        await fixture.Service.ProcessPendingAsync(fixture.CompanyId);

        fixture.Context.ChangeTracker.Clear();
        var sent = await fixture.Context.ElectronicInvoices.SingleAsync();
        sent.Status.Should().Be(EcfStatus.Sent);
        sent.NextCheckAt.Should().NotBeNull();
        sent.NextCheckAt.Should().BeOnOrBefore(DateTime.UtcNow);

        fixture.Context.ChangeTracker.Clear();

        var retrack = await fixture.Service.RetrackAsync(fixture.CompanyId);

        retrack.Should().BeOfType<Either<Error, int>.Right>();
        fixture.Dgii.StatusRequests.Should().Be(1);

        fixture.Context.ChangeTracker.Clear();
        var accepted = await fixture.Context.ElectronicInvoices.SingleAsync();
        accepted.Status.Should().Be(EcfStatus.Accepted);
        fixture.Dgii.StatusRequests.Should().Be(1);
    }

    [Fact]
    public async Task TheDatabaseRefusesTwoDocumentsForTheSameSale()
    {
        await using var fixture = new EmissionFixture();
        await fixture.InitializeAsync();

        await fixture.Service.IssueAsync(fixture.SaleId, fixture.CompanyId);

        var second = new ElectronicInvoice
        {
            Id = Guid.CreateVersion7(),
            CompanyId = fixture.CompanyId,
            SaleId = fixture.SaleId,
            ENCF = "E320000000002",
            DocumentType = EcfDocumentType.Consumer,
            IssuerTaxId = "10100000101",
            SignedXml = "<ECF />",
            Status = EcfStatus.Draft
        };

        fixture.Context.ElectronicInvoices.Add(second);

        var act = async () => await fixture.Context.SaveChangesAsync();

        // One sale, one document: two of them would burn two fiscal numbers
        // for the same sale.
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task IssueAsync_WithTheMigratedDemoRange_EmitsTheNextNumber()
    {
        await using var fixture = new EmissionFixture(consumerSequence: 903);
        await fixture.InitializeAsync();

        var result = await fixture.Service.IssueAsync(fixture.SaleId, fixture.CompanyId);

        result.Should().BeOfType<Either<Error, ElectronicInvoiceDto>.Right>();
        ((Either<Error, ElectronicInvoiceDto>.Right)result).Value.ENCF.Should().Be("E320000000904");
    }

    [Fact]
    public async Task IssueAsync_TwiceForTwoSalesOfTheSameIssuer_TakesTwoConsecutiveNumbers()
    {
        await using var fixture = new EmissionFixture();
        await fixture.InitializeAsync();

        var first = await fixture.Service.IssueAsync(fixture.SaleId, fixture.CompanyId);
        var second = await fixture.Service.IssueAsync(fixture.SecondSaleId, fixture.CompanyId);

        ((Either<Error, ElectronicInvoiceDto>.Right)first).Value.ENCF.Should().Be("E320000000001");
        ((Either<Error, ElectronicInvoiceDto>.Right)second).Value.ENCF.Should().Be("E320000000002");

        var invoices = await fixture.Context.ElectronicInvoices.OrderBy(i => i.ENCF).ToListAsync();
        invoices.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(EcfDocumentType.DebitNote)]
    [InlineData(EcfDocumentType.CreditNote)]
    public async Task IssueAsync_ForANoteThatModifiesADocument_WritesTheInformationReference(EcfDocumentType documentType)
    {
        await using var fixture = new EmissionFixture();
        await fixture.InitializeAsync();

        var noteId = await fixture.AddNoteAsync(
            documentType,
            "E310000000901", new DateTime(2026, 10, 1), EcfModificationCode.CorrectsAmounts, "Ajuste del monto");

        var result = await fixture.Service.IssueAsync(noteId, fixture.CompanyId);

        result.Should().BeOfType<Either<Error, ElectronicInvoiceDto>.Right>();

        var invoice = await fixture.Context.ElectronicInvoices.SingleAsync(i => i.SaleId == noteId);
        invoice.DocumentType.Should().Be(documentType);
        invoice.SignedXml.Should().Contain("<NCFModificado>E310000000901</NCFModificado>");
        invoice.SignedXml.Should().Contain("<CodigoModificacion>3</CodigoModificacion>");
        invoice.SignedXml.Should().Contain("<RazonModificacion>Ajuste del monto</RazonModificacion>");

        if (documentType == EcfDocumentType.CreditNote)
            invoice.SignedXml.Should().Contain("<IndicadorNotaCredito>1</IndicadorNotaCredito>");
    }

    [Theory]
    [InlineData(EcfDocumentType.DebitNote)]
    [InlineData(EcfDocumentType.CreditNote)]
    public async Task IssueAsync_ForANoteWithoutAReference_FailsClosed(EcfDocumentType documentType)
    {
        await using var fixture = new EmissionFixture();
        await fixture.InitializeAsync();

        var noteId = await fixture.AddNoteAsync(
            documentType,
            referencedEncf: null,
            referencedAt: null,
            modificationCode: null);

        var result = await fixture.Service.IssueAsync(noteId, fixture.CompanyId);

        result.Should().BeOfType<Either<Error, ElectronicInvoiceDto>.Left>();
        var error = ((Either<Error, ElectronicInvoiceDto>.Left)result).Value;
        error.Message.Should().Contain("modifies");

        (await fixture.Context.ElectronicInvoices.CountAsync()).Should().Be(0);
        (await fixture.Context.ElectronicInvoiceSequences
            .Where(s => s.DocumentType == documentType)
            .Select(s => s.CurrentSequence)
            .SingleAsync()).Should().Be(0,
            "a note without a reference does not take a number");
        fixture.Dgii.SendRequests.Should().Be(0);
    }

    [Fact]
    public async Task IssueAsync_WithASaleOfAnotherCompany_DoesNotIssueIt()
    {
        await using var fixture = new EmissionFixture();
        await fixture.InitializeAsync();

        // The emission runs without an HTTP context, so the global tenant
        // filter is off and the sale of another company is readable: the
        // company travels by hand, and it is what stops the emission.
        var result = await fixture.Service.IssueAsync(fixture.OtherSaleId, fixture.CompanyId);

        result.Should().BeOfType<Either<Error, ElectronicInvoiceDto>.Left>();
        var error = ((Either<Error, ElectronicInvoiceDto>.Left)result).Value;
        error.Message.Should().Contain("company");

        (await fixture.Context.ElectronicInvoices.CountAsync()).Should().Be(0);
        (await fixture.Context.ElectronicInvoiceSequences
            .Where(s => s.DocumentType == EcfDocumentType.Consumer)
            .Select(s => s.CurrentSequence)
            .SingleAsync()).Should().Be(0,
            "a refused emission does not take a number from the issuer");
        fixture.Dgii.SendRequests.Should().Be(0);
    }

    [Fact]
    public async Task TheDueDocumentsAreTheOnesOfTheCompanyWhoseAppointmentPassed()
    {
        await using var fixture = new EmissionFixture();
        await fixture.InitializeAsync();

        fixture.Context.ElectronicInvoices.AddRange(
            Invoice(fixture.CompanyId, "10100000101", fixture.SaleId, "E320000000777", DateTime.UtcNow.AddMinutes(-1)),
            Invoice(fixture.CompanyId, "10100000101", fixture.SecondSaleId, "E320000000778", DateTime.UtcNow.AddMinutes(30)),
            Invoice(fixture.OtherCompanyId, "10100000102", fixture.OtherSaleId, "E320000000779", DateTime.UtcNow.AddMinutes(-1)));

        await fixture.Context.SaveChangesAsync();

        var due = await fixture.UnitOfWork.ElectronicInvoices.GetDueForCheckAsync(fixture.CompanyId, DateTime.UtcNow);

        // The appointment of another company is not ours, and the one that is
        // not due yet waits: the retrack does not ask about it.
        due.Should().ContainSingle().Which.ENCF.Should().Be("E320000000777");
    }

    private static ElectronicInvoice Invoice(
        Guid companyId, string issuerRnc, Guid saleId, string encf, DateTime nextCheckAt) => new()
    {
        Id = Guid.CreateVersion7(),
        CompanyId = companyId,
        SaleId = saleId,
        ENCF = encf,
        DocumentType = EcfDocumentType.Consumer,
        IssuerTaxId = issuerRnc,
        SignedXml = "<ECF />",
        Status = EcfStatus.InProcess,
        NextCheckAt = nextCheckAt,
        StatusCheckCount = 1
    };

    private sealed class EmissionFixture : IAsyncDisposable
    {
        private const string IssuerRnc = "10100000101";
        private readonly SqliteConnection _connection;
        private readonly int _consumerSequence;

        public EmissionFixture(int consumerSequence = 0)
        {
            _consumerSequence = consumerSequence;
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            Options = new DbContextOptionsBuilder<VolterpDbContext>()
                .UseSqlite(_connection)
                .Options;

            Context = new VolterpDbContext(Options);
        }

        public DbContextOptions<VolterpDbContext> Options { get; }
        public VolterpDbContext Context { get; }
        public DgiiStub Dgii { get; } = new();
        public Mock<ISigningCertificateProvider> CertificateProvider { get; } = new();
        public Guid CompanyId { get; private set; }
        public Guid OtherCompanyId { get; private set; }
        public Guid SaleId { get; private set; }
        public Guid SecondSaleId { get; private set; }
        public Guid OtherSaleId { get; private set; }
        public IElectronicInvoiceService Service { get; private set; } = null!;
        public UnitOfWork UnitOfWork { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            await Context.Database.EnsureCreatedAsync();

            CompanyId = Guid.CreateVersion7();
            OtherCompanyId = Guid.CreateVersion7();

            Context.Companies.AddRange(
                Company(CompanyId, IssuerRnc),
                Company(OtherCompanyId, "10100000102"));

            SaleId = Guid.CreateVersion7();
            SecondSaleId = Guid.CreateVersion7();
            OtherSaleId = Guid.CreateVersion7();

            Context.Sales.AddRange(
                Sale(SaleId, CompanyId, "V-0001"),
                Sale(SecondSaleId, CompanyId, "V-0002"),
                Sale(OtherSaleId, OtherCompanyId, "V-0003"));

            Context.ElectronicInvoiceSequences.AddRange(
                Range(CompanyId, IssuerRnc, EcfDocumentType.Consumer, _consumerSequence),
                Range(CompanyId, IssuerRnc, EcfDocumentType.FiscalCredit),
                Range(CompanyId, IssuerRnc, EcfDocumentType.DebitNote),
                Range(CompanyId, IssuerRnc, EcfDocumentType.CreditNote));

            await Context.SaveChangesAsync();

            AllowSigning();

            var signer = new XmlDsigEcfSigner();
            var authenticator = new DgiiAuthenticator(Dgii, signer, new DgiiTokenCache());

            UnitOfWork = new UnitOfWork(Context);

            Service = new ElectronicInvoiceService(
                UnitOfWork,
                new EcfXmlWriter(),
                signer,
                CertificateProvider.Object,
                authenticator,
                Dgii);
        }

        public void AllowSigning()
        {
            CertificateProvider
                .Setup(c => c.GetSigningCertificateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateCertificate());
        }

        private static Company Company(Guid id, string taxId) => new()
        {
            Id = id,
            Name = "Distribuidora del Norte",
            LegalName = "Distribuidora del Norte SRL",
            TaxId = taxId,
            Address = "Calle Principal 1, Santo Domingo",
            Phone = "809-555-1234",
            Email = "facturacion@distribuidora.do"
        };

        public async Task<Guid> AddNoteAsync(
            EcfDocumentType documentType,
            string? referencedEncf,
            DateTime? referencedAt,
            EcfModificationCode? modificationCode,
            string? modificationReason = null)
        {
            var note = new Sale
            {
                Id = Guid.CreateVersion7(),
                CompanyId = CompanyId,
                Code = $"V-NOTE-{Guid.NewGuid():N}"[..20],
                Status = SaleStatus.Completed,
                DocumentType = documentType,
                ReceptorTaxId = "101010101",
                ClienteName = "Cliente de Prueba SRL",
                CreatedAt = new DateTime(2026, 11, 4),
                ModifiedENCF = referencedEncf,
                ModifiedAt = referencedAt,
                ModificationCode = modificationCode,
                ModificationReason = modificationReason
            };

            note.AddItem(Guid.CreateVersion7(), "Café molido", 2, 100m, TaxCode.ItbisTasa1);

            Context.Sales.Add(note);
            await Context.SaveChangesAsync();

            return note.Id;
        }

        // The range the DGII granted to the issuer: 1 to 10,000,000, and 1 to
        // 50,000,000 for the consumer invoice, which carries no expiry date.
        private static ElectronicInvoiceSequence Range(
            Guid companyId,
            string issuerTaxId,
            EcfDocumentType documentType,
            int currentSequence = 0) => new()
        {
            Id = Guid.CreateVersion7(),
            CompanyId = companyId,
            IssuerTaxId = issuerTaxId,
            DocumentType = documentType,
            From = 1,
            To = documentType == EcfDocumentType.Consumer ? 50_000_000 : 10_000_000,
            ExpiresAt = documentType is EcfDocumentType.Consumer or EcfDocumentType.CreditNote
                ? null
                : new DateTime(2028, 12, 31),
            CurrentSequence = currentSequence
        };

        private static Sale Sale(Guid id, Guid companyId, string code)
        {
            var sale = new Sale
            {
                Id = id,
                CompanyId = companyId,
                Code = code,
                Status = SaleStatus.Completed,
                DocumentType = EcfDocumentType.Consumer,
                CreatedAt = new DateTime(2026, 11, 4)
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

    // The DGII as the tests see it: it answers the seed, the token, the
    // acknowledgement and the verdict.
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
