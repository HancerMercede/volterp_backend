using System.Net;
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

// Independent probes of T47 (read-only verifier): what the DGII answered stays
// with the document, what it does not say never becomes a false, and the reason
// of a failure never takes the place of the answer.
[Collection("VolterpApiFactory")]
public class T47VerifierProbeTests
{
    private const string IssuerRnc = "10100000101";

    private const string Acknowledgement = """{"trackId":"vk-1","error":null,"mensaje":"Recibido"}""";

    private const string VerdictWithSequence =
        """{"trackId":"vk-1","codigo":3,"estado":"En proceso","secuenciaUtilizada":true,"fechaRecepcion":"2026-11-14T10:00:00Z","mensajes":[{"valor":"En proceso","codigo":3}]}""";

    private const string VerdictWithoutSequence =
        """{"trackId":"vk-1","codigo":3,"estado":"En proceso","mensajes":[{"valor":"En proceso","codigo":3}]}""";

    private const string Rejection =
        """{"codigo":2,"estado":"Rechazado","secuenciaUtilizada":true,"mensajes":[{"valor":"El RNC del receptor no existe","codigo":7}]}""";

    private const string TimeoutBody = """{"error":"timeout","codigo":503}""";

    [Fact]
    public async Task TheAcknowledgementOfTheSend_StaysOnTheDocument()
    {
        await using var fixture = new ProbeFixture();
        await fixture.InitializeAsync();

        await fixture.Service.IssueAsync(fixture.SaleId, fixture.CompanyId);
        await fixture.Service.ProcessPendingAsync(fixture.CompanyId);

        var document = await fixture.Invoice();

        document.RawResponse.Should().Be(Acknowledgement);
    }

    [Fact]
    public async Task TheAnswerOfTheVerdict_StaysOnTheDocument()
    {
        await using var fixture = new ProbeFixture();
        await fixture.InitializeAsync();

        await fixture.Service.IssueAsync(fixture.SaleId, fixture.CompanyId);
        await fixture.Service.ProcessPendingAsync(fixture.CompanyId);
        fixture.Context.ChangeTracker.Clear();
        await fixture.Service.RetrackAsync(fixture.CompanyId);

        var document = await fixture.Invoice();

        document.RawResponse.Should().Be(VerdictWithSequence);
        document.SequenceConsumed.Should().BeTrue();
        document.ReceivedAt.Should().Be(new DateTime(2026, 11, 14, 10, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task TheBodyOfAFailedSend_StaysOnTheDocument()
    {
        await using var fixture = new ProbeFixture();
        await fixture.InitializeAsync();

        fixture.Dgii.SendFailure = new DgiiException(
            "The DGII rejected the e-CF file (BadRequest).",
            Rejection,
            statusCode: HttpStatusCode.BadRequest);

        await fixture.Service.IssueAsync(fixture.SaleId, fixture.CompanyId);
        await fixture.Service.ProcessPendingAsync(fixture.CompanyId);

        var document = await fixture.Invoice();

        document.RawResponse.Should().Be(Rejection, "the body of the failed conversation is kept");
        document.Status.Should().Be(EcfStatus.Rejected);
        document.VerdictNote.Should().Contain("El RNC del receptor no existe");
    }

    [Fact]
    public async Task TheBodyOfAFailedCheck_StaysOnTheDocument()
    {
        await using var fixture = new ProbeFixture();
        await fixture.InitializeAsync();

        await fixture.Service.IssueAsync(fixture.SaleId, fixture.CompanyId);
        await fixture.Service.ProcessPendingAsync(fixture.CompanyId);
        fixture.Context.ChangeTracker.Clear();
        await fixture.Service.RetrackAsync(fixture.CompanyId);

        fixture.Dgii.StatusFailure = new DgiiException(
            "The DGII failed to answer the status of vk-1 (ServiceUnavailable).",
            TimeoutBody,
            statusCode: HttpStatusCode.ServiceUnavailable);

        await fixture.AppointmentNow();
        fixture.Context.ChangeTracker.Clear();
        await fixture.Service.RetrackAsync(fixture.CompanyId);

        var document = await fixture.Invoice();

        document.RawResponse.Should().Be(TimeoutBody,
            "the body of the failed conversation is kept");
        document.LastError.Should().Contain("ServiceUnavailable");
    }

    [Fact]
    public async Task AVerdictWithoutSecuenciaUtilizada_LeavesTheColumnInNull()
    {
        await using var fixture = new ProbeFixture();
        await fixture.InitializeAsync();

        fixture.Dgii.VerdictBody = VerdictWithoutSequence;
        fixture.Dgii.SequenceConsumed = null;

        await fixture.Service.IssueAsync(fixture.SaleId, fixture.CompanyId);
        await fixture.Service.ProcessPendingAsync(fixture.CompanyId);
        fixture.Context.ChangeTracker.Clear();
        await fixture.Service.RetrackAsync(fixture.CompanyId);

        var document = await fixture.Invoice();

        document.RawResponse.Should().Be(VerdictWithoutSequence);
        document.SequenceConsumed.Should().BeNull("the DGII did not say it: null is not false");
    }

    // The third conversation: the answer of the send was lost, so the DGII is
    // asked whether it has the document. That answer carries the day it
    // received it and nothing else.
    [Fact]
    public async Task TheLookupByEncf_StoresTheDayTheDgiiReceivedTheDocument()
    {
        await using var fixture = new ProbeFixture();
        await fixture.InitializeAsync();

        // The document is signed and has no track id: the answer of the send
        // was lost.
        await fixture.Service.IssueAsync(fixture.SaleId, fixture.CompanyId);
        fixture.Context.ChangeTracker.Clear();

        fixture.Dgii.LookupReceivedAt = new DateTimeOffset(2026, 11, 14, 10, 0, 0, TimeSpan.Zero);

        // The verdict does not say the day: the one that says it is the
        // lookup.
        fixture.Dgii.StatusReceivedAt = null;

        var recovered = await fixture.Service.RecoverAsync(fixture.CompanyId, IssuerRnc, "E320000000001");

        recovered.Should().BeOfType<Either<Error, ElectronicInvoiceDto>.Right>();

        var document = await fixture.Invoice();

        document.TrackId.Should().Be("vk-1", "the track id the DGII gave is adopted");
        document.ReceivedAt.Should().Be(new DateTime(2026, 11, 14, 10, 0, 0, DateTimeKind.Utc),
            "the lookup is the conversation that says the day the DGII received the document");
    }

    [Fact]
    public async Task WhatTheDgiiDoesNotSay_DoesNotEraseWhatItSaidBefore()
    {
        await using var fixture = new ProbeFixture();
        await fixture.InitializeAsync();

        await fixture.Service.IssueAsync(fixture.SaleId, fixture.CompanyId);
        await fixture.Service.ProcessPendingAsync(fixture.CompanyId);
        fixture.Context.ChangeTracker.Clear();
        await fixture.Service.RetrackAsync(fixture.CompanyId);

        var afterTheFirstAnswer = await fixture.Invoice();
        afterTheFirstAnswer.SequenceConsumed.Should().BeTrue();

        // The second conversation does not mention the sequence.
        fixture.Dgii.VerdictBody = VerdictWithoutSequence;
        fixture.Dgii.SequenceConsumed = null;

        await fixture.AppointmentNow();
        fixture.Context.ChangeTracker.Clear();
        await fixture.Service.RetrackAsync(fixture.CompanyId);

        var afterTheSecondAnswer = await fixture.Invoice();
        afterTheSecondAnswer.SequenceConsumed.Should().BeTrue(
            "the DGII did not say it this time, so what it said before still stands");
        afterTheSecondAnswer.RawResponse.Should().Be(VerdictWithoutSequence);

        // The third conversation comes without a body at all.
        fixture.Dgii.VerdictBody = null;

        await fixture.AppointmentNow();
        fixture.Context.ChangeTracker.Clear();
        await fixture.Service.RetrackAsync(fixture.CompanyId);

        var afterTheThirdAnswer = await fixture.Invoice();
        afterTheThirdAnswer.RawResponse.Should().Be(VerdictWithoutSequence,
            "the body of the last answer survives a conversation without a body");
        afterTheThirdAnswer.SequenceConsumed.Should().BeTrue();
    }

    [Fact]
    public async Task TheReasonOfTheFailure_LivesBesideTheAnswerOfTheDgii()
    {
        await using var fixture = new ProbeFixture();
        await fixture.InitializeAsync();

        await fixture.Service.IssueAsync(fixture.SaleId, fixture.CompanyId);
        await fixture.Service.ProcessPendingAsync(fixture.CompanyId);
        fixture.Context.ChangeTracker.Clear();
        await fixture.Service.RetrackAsync(fixture.CompanyId);

        // What the DGII said before is on the document.
        await fixture.SetVerdictNote("Lo que dijo la DGII antes.");

        fixture.Dgii.StatusFailure = new HttpRequestException("The DGII did not answer.");

        await fixture.AppointmentNow();
        fixture.Context.ChangeTracker.Clear();
        await fixture.Service.RetrackAsync(fixture.CompanyId);

        var document = await fixture.Invoice();

        document.VerdictNote.Should().Be("Lo que dijo la DGII antes.",
            "the reason of the failure does not take the place of the answer");
        document.LastError.Should().Contain("The DGII did not answer.");
        document.RawResponse.Should().Be(VerdictWithSequence);
    }

    private sealed class ProbeFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        public ProbeFixture()
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

            sale.AddItem(Guid.CreateVersion7(), "Cafe molido", 2, 100m, TaxCode.ItbisTasa1);
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

        public async Task SetVerdictNote(string note)
        {
            await Context.ElectronicInvoices
                .ExecuteUpdateAsync(setters => setters.SetProperty(i => i.VerdictNote, note));
        }

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

    private sealed class DgiiStub : IDgiiClient
    {
        public DgiiVerdict Verdict { get; set; } = DgiiVerdict.InProcess;

        public string? VerdictBody { get; set; } = VerdictWithSequence;

        public bool? SequenceConsumed { get; set; } = true;

        public DateTimeOffset? StatusReceivedAt { get; set; } = new DateTimeOffset(2026, 11, 14, 10, 0, 0, TimeSpan.Zero);

        public DateTimeOffset? LookupReceivedAt { get; set; } = DateTimeOffset.UtcNow;

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

            return Task.FromResult(new DgiiReceipt("vk-1", null, null) { RawBody = Acknowledgement });
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
                SequenceConsumed,
                StatusReceivedAt,
                [new DgiiMessage("En proceso", 3)])
            { RawBody = VerdictBody });
        }

        public Task<DgiiTrackLookup> GetTrackIdAsync(string issuerTaxId, string encf, string token, CancellationToken cancellationToken = default)
            => Task.FromResult(new DgiiTrackLookup("vk-1", "En proceso", LookupReceivedAt));
    }
}
