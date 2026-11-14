using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
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

// What the DGII answered stays with the document: the acknowledgement of the
// send, the verdict and the reason of a failure live side by side, and none
// of them wipes the other.
public class ElectronicInvoiceDgiiResponseTests
{
    private const string Acknowledgement = """{"trackId":"track-1","error":null,"mensaje":"Recibido"}""";

    private const string AcceptedVerdict =
        """{"trackId":"track-1","codigo":1,"estado":"Aceptado","rnc":"10100000101","eNCF":"E320000000001","secuenciaUtilizada":true,"fechaRecepcion":"2026-11-14T10:00:00Z","mensajes":[{"valor":"Aceptado","codigo":1}]}""";

    private const string Rejection =
        """{"codigo":2,"estado":"Rechazado","secuenciaUtilizada":true,"mensajes":[{"valor":"El RNC del receptor no existe","codigo":7}]}""";

    [Fact]
    public async Task TheAcknowledgementOfTheSend_StaysOnTheDocument()
    {
        await using var fixture = new ResponseFixture();
        await fixture.InitializeAsync();

        await fixture.Service.IssueAsync(fixture.SaleId, fixture.CompanyId);
        await fixture.Service.ProcessPendingAsync(fixture.CompanyId);

        var sent = await fixture.Invoice();

        // The body of the acknowledgement is what the DGII answered when it
        // received the document.
        sent.RawResponse.Should().Be(Acknowledgement);
    }

    [Fact]
    public async Task TheAnswerOfTheVerdict_StaysOnTheDocument()
    {
        await using var fixture = new ResponseFixture();
        await fixture.InitializeAsync();

        await fixture.Service.IssueAsync(fixture.SaleId, fixture.CompanyId);
        await fixture.Service.ProcessPendingAsync(fixture.CompanyId);

        // The retrack reads the documents without tracking them: the tracker
        // of the test is cleared so the document can be written back.
        fixture.Context.ChangeTracker.Clear();

        await fixture.Service.RetrackAsync(fixture.CompanyId);

        var accepted = await fixture.Invoice();

        // The body of the answer carries the codes of the messages of the
        // DGII, and the two fiscal facts travel beside it.
        accepted.RawResponse.Should().Be(AcceptedVerdict);
        accepted.SequenceConsumed.Should().BeTrue(
            "the DGII said the number cannot be used again");
        accepted.ReceivedAt.Should().Be(
            new DateTime(2026, 11, 14, 10, 0, 0, DateTimeKind.Utc),
            "the DGII said when it received the document");
    }

    [Fact]
    public async Task TheRejectionOfTheDgii_StaysOnTheDocument()
    {
        await using var fixture = new ResponseFixture();
        await fixture.InitializeAsync();

        fixture.Dgii.SendFailure = new DgiiException(
            "The DGII rejected the e-CF file (BadRequest).",
            Rejection,
            statusCode: HttpStatusCode.BadRequest);

        await fixture.Service.IssueAsync(fixture.SaleId, fixture.CompanyId);
        await fixture.Service.ProcessPendingAsync(fixture.CompanyId);

        var rejected = await fixture.Invoice();

        // The rejection is a conversation too: its body says why.
        rejected.Status.Should().Be(EcfStatus.Rejected);
        rejected.RawResponse.Should().Be(Rejection);
        rejected.VerdictNote.Should().Contain("El RNC del receptor no existe");
    }

    [Fact]
    public async Task AFailedCheck_LeavesTheAnswerOfTheDgiiAlone()
    {
        await using var fixture = new ResponseFixture();
        await fixture.InitializeAsync();

        fixture.Dgii.Verdict = DgiiVerdict.InProcess;
        fixture.Dgii.VerdictBody = """{"trackId":"track-1","codigo":3,"estado":"En proceso","secuenciaUtilizada":false,"mensajes":[]}""";

        await fixture.Service.IssueAsync(fixture.SaleId, fixture.CompanyId);
        await fixture.Service.ProcessPendingAsync(fixture.CompanyId);

        fixture.Context.ChangeTracker.Clear();

        await fixture.Service.RetrackAsync(fixture.CompanyId);

        fixture.Dgii.StatusFailure = new HttpRequestException("The DGII did not answer.");

        // The document is asked again: the appointment is brought forward.
        await fixture.AppointmentNow();

        fixture.Context.ChangeTracker.Clear();

        await fixture.Service.RetrackAsync(fixture.CompanyId);

        var document = await fixture.Invoice();

        // The reason of the failure lives beside the answer of the DGII: one
        // does not wipe the other.
        document.LastError.Should().Contain("The DGII did not answer.");
        document.RawResponse.Should().Be(fixture.Dgii.VerdictBody,
            "the answer of the DGII is still the last thing it said");
    }

    private sealed class ResponseFixture : IAsyncDisposable
    {
        private const string IssuerRnc = "10100000101";

        private readonly SqliteConnection _connection;

        public ResponseFixture()
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
        public DgiiStub Dgii { get; } = new();
        public Guid CompanyId { get; private set; }
        public Guid SaleId { get; private set; }
        public IElectronicInvoiceService Service { get; private set; } = null!;
        public UnitOfWork UnitOfWork { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            await Context.Database.EnsureCreatedAsync();

            CompanyId = Guid.CreateVersion7();
            SaleId = Guid.CreateVersion7();

            Context.Companies.Add(new Company
            {
                Id = CompanyId,
                Name = "Distribuidora del Norte",
                LegalName = "Distribuidora del Norte SRL",
                TaxId = IssuerRnc,
                Address = "Calle Principal 1, Santo Domingo",
                Phone = "809-555-1234",
                Email = "facturacion@distribuidora.do"
            });

            var sale = new Sale
            {
                Id = SaleId,
                CompanyId = CompanyId,
                Code = "V-0001",
                Status = SaleStatus.Completed,
                DocumentType = EcfDocumentType.Consumer,
                CreatedAt = new DateTime(2026, 11, 4)
            };

            sale.AddItem(Guid.CreateVersion7(), "Café molido", 2, 100m, TaxCode.ItbisTasa1);
            Context.Sales.Add(sale);

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

            var certificates = new Mock<ISigningCertificateProvider>();
            certificates.Setup(c => c.GetSigningCertificateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateCertificate());

            var signer = new XmlDsigEcfSigner();

            UnitOfWork = new UnitOfWork(Context);

            Service = new ElectronicInvoiceService(
                UnitOfWork,
                new EcfXmlWriter(),
                signer,
                certificates.Object,
                new DgiiAuthenticator(Dgii, signer, new DgiiTokenCache()),
                Dgii);
        }

        public async Task<ElectronicInvoice> Invoice()
        {
            Context.ChangeTracker.Clear();

            return await Context.ElectronicInvoices.SingleAsync();
        }

        // The appointment of the next check is brought forward so the
        // document is asked again.
        public async Task AppointmentNow()
        {
            await Context.ElectronicInvoices
                .ExecuteUpdateAsync(setters => setters.SetProperty(i => i.NextCheckAt, DateTime.UtcNow.AddMinutes(-1)));
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

    // The DGII as the tests see it: it acknowledges the document, answers the
    // verdict and can fail the way the network fails.
    private sealed class DgiiStub : IDgiiClient
    {
        public DgiiVerdict Verdict { get; set; } = DgiiVerdict.Accepted;

        public string VerdictBody { get; set; } = AcceptedVerdict;

        public Exception? SendFailure { get; set; }

        public Exception? StatusFailure { get; set; }

        public Task<string> GetSeedAsync(CancellationToken cancellationToken = default)
            => Task.FromResult("<SemillaModel><valor>abc</valor><fecha>2019-03-13T14:33:32-04:00</fecha></SemillaModel>");

        public Task<DgiiToken> RequestTokenAsync(string signedSeedXml, CancellationToken cancellationToken = default)
            => Task.FromResult(new DgiiToken("jwt", DateTimeOffset.UtcNow.AddHours(1), DateTimeOffset.UtcNow));

        public Task<DgiiReceipt> SendElectronicInvoiceAsync(string signedEcfXml, string token, CancellationToken cancellationToken = default)
        {
            if (SendFailure is not null)
                throw SendFailure;

            return Task.FromResult(new DgiiReceipt("track-1", null, null) { RawBody = Acknowledgement });
        }

        public Task<DgiiStatus> GetStatusAsync(string trackId, string token, CancellationToken cancellationToken = default)
        {
            if (StatusFailure is not null)
                throw StatusFailure;

            return Task.FromResult(new DgiiStatus(
                trackId,
                Verdict,
                Verdict.ToString(),
                "10100000101",
                "E320000000001",
                true,
                new DateTimeOffset(2026, 11, 14, 10, 0, 0, TimeSpan.Zero),
                [new DgiiMessage("Aceptado", 1)])
            { RawBody = VerdictBody });
        }

        public Task<DgiiTrackLookup> GetTrackIdAsync(string issuerTaxId, string encf, string token, CancellationToken cancellationToken = default)
            => Task.FromResult(new DgiiTrackLookup("track-1", "Aceptado", DateTimeOffset.UtcNow));
    }
}
