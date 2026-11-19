using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using EitherWay;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Volterp.Application.DTOs.ElectronicInvoicePrintDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Interfaces;
using Volterp.Application.Services;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.ElectronicInvoicing;
using Volterp.ElectronicInvoicing.Signing;
using Volterp.ElectronicInvoicing.Xml;
using Volterp.Infrastructure.Data;
using Volterp.Infrastructure.UnitOfWork;
using Xunit;

namespace Volterp.Tests.Integration;

// The print service as the facade hands it over: the same representation the
// endpoint prints, reached through the facade.
public class ElectronicInvoicePrintRepositoryTests : IAsyncDisposable
{
    private const string IssuerTaxId = "130862346";
    private const string Encf = "E310000000001";
    private const string TimbreUrl =
        "https://ecf.dgii.gov.do/testecf/consultatimbre?rncemisor=130862346&encf=E310000000001";

    private readonly SqliteConnection _connection;
    private readonly VolterpDbContext _context;

    private Guid _companyId;
    private Guid _invoiceId;
    private string _signedXml = string.Empty;

    public ElectronicInvoicePrintRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _context = new VolterpDbContext(
            new DbContextOptionsBuilder<VolterpDbContext>()
                .UseSqlite(_connection)
                .Options);
    }

    [Fact]
    public async Task TheFacadeHandsThePrintServiceOverAndItPrintsTheSignedDocument()
    {
        await SeedAsync();

        var services = new ServiceManager(new UnitOfWork(_context), null!, null!, null!, null!);

        // The facade is the only way in: the service is the one the facade
        // builds, and it is the same one on every call.
        services.ElectronicInvoicePrints.Should().NotBeNull();
        services.ElectronicInvoicePrints.Should().BeSameAs(services.ElectronicInvoicePrints);

        var result = await services.ElectronicInvoicePrints.PrintAsync(_invoiceId, _companyId);

        result.Should().BeOfType<Either<Error, ElectronicInvoicePrintDto>.Right>();
        var print = ((Either<Error, ElectronicInvoicePrintDto>.Right)result).Value;

        // The representation carries what the document that was signed carries.
        print.ElectronicInvoiceId.Should().Be(_invoiceId);
        print.ENCF.Should().Be(Encf);
        print.DocumentType.Should().Be(EcfDocumentType.FiscalCredit);
        print.QrUrl.Should().Be(TimbreUrl);
        print.SecurityCode.Should().Be(EcfSecurityCode.From(_signedXml));
        print.Document.Issuer.TaxId.Should().Be(IssuerTaxId);
        print.Document.Issuer.BusinessName.Should().Be("Company A Legal");
        print.Document.Totals.Total.Should().Be(236m);
    }

    private async Task SeedAsync()
    {
        await _context.Database.EnsureCreatedAsync();

        _companyId = Guid.CreateVersion7();
        _invoiceId = Guid.CreateVersion7();

        var saleId = Guid.CreateVersion7();

        _context.Companies.Add(new Company
        {
            Id = _companyId,
            Name = "Company A",
            TaxId = IssuerTaxId,
            Address = "1 A Street",
            LegalName = "El Nombre De La Empresa Hoy",
            Email = "a@company.test",
            Phone = "555-0001"
        });

        _context.Sales.Add(new Sale
        {
            Id = saleId,
            CompanyId = _companyId,
            Code = "V-0101",
            Status = SaleStatus.Completed,
            DocumentType = EcfDocumentType.FiscalCredit,
            ReceptorTaxId = "130862354",
            Subtotal = 200m,
            Itbis = 36m,
            Total = 236m
        });

        var signedXml = SignedXml();
        _signedXml = signedXml;

        _context.ElectronicInvoices.Add(new ElectronicInvoice
        {
            Id = _invoiceId,
            CompanyId = _companyId,
            SaleId = saleId,
            ENCF = Encf,
            DocumentType = EcfDocumentType.FiscalCredit,
            IssuerTaxId = IssuerTaxId,
            ReceiverTaxId = "130862354",
            SignedXml = signedXml,
            SecurityCode = EcfSecurityCode.From(signedXml),
            QrUrl = TimbreUrl,
            SignedAt = new DateTime(2026, 11, 14, 10, 30, 0),
            Status = EcfStatus.Accepted,
            Subtotal = 200m,
            Itbis = 36m,
            Total = 236m
        });

        await _context.SaveChangesAsync();
    }

    // The document the seed signs: the writer produces the XML and the signer
    // signs it, so the representation is built from a real signature.
    private static string SignedXml()
    {
        var items = new[]
        {
            new EcfItem
            {
                LineNumber = 1,
                TaxTreatment = TaxCode.ItbisTasa1,
                Name = "Café molido",
                Kind = EcfItemKind.Goods,
                Quantity = 2.5m,
                UnitPrice = 80m,
                Amount = 200m
            }
        };

        var document = new EcfDocument
        {
            DocumentType = EcfDocumentType.FiscalCredit,
            Identity = new EcfIdentity
            {
                LineAmountsIncludeItbis = false,
                ENCF = Encf,
                SequenceExpiresAt = new DateTime(2028, 12, 31),
                TypeIncome = EcfTypeIncome.Operations,
                PaymentType = EcfPaymentType.Cash
            },
            Issuer = new EcfIssuer
            {
                TaxId = IssuerTaxId,
                BusinessName = "Company A Legal",
                TradeName = "Company A Commercial Name",
                Branch = "Sucursal Principal",
                Address = "Signed Issuer Address",
                Municipality = "010100",
                Province = "010000",
                IssuedAt = new DateTime(2026, 11, 14)
            },
            Buyer = new EcfBuyer { TaxId = "130862354", BusinessName = "Comprador SRL" },
            Totals = EcfTotalsCalculator.Calculate(items),
            Items = items,
            SignedAt = new DateTime(2026, 11, 14, 10, 30, 0)
        };

        var xml = new EcfXmlWriter().Write(document);

        return new XmlDsigEcfSigner().Sign(xml, CreateCertificate());
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
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
