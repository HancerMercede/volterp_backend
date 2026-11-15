using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.ElectronicInvoicing;
using Volterp.ElectronicInvoicing.Signing;
using Volterp.ElectronicInvoicing.Xml;
using Volterp.Infrastructure.Data;
using Volterp.Tests.TestHelpers;
using Xunit;

namespace Volterp.Tests.Integration;

// The printed representation of an e-CF as the tenant asks for it: the HTML
// the browser prints, with the QR of the timbre and the security code of the
// signature.
[Collection("VolterpApiFactory")]
public class ElectronicInvoicePrintEndpointTests : IClassFixture<ApiFactory>
{
    private const string SignedEncf = "E310000000001";
    private const string ConsumerEncf = "E320000000002";
    private const string DraftEncf = "E320000000003";
    private const string InProcessEncf = "E310000000006";
    private const string RejectedEncf = "E310000000005";
    private const string AnnulledEncf = "E310000000007";
    private const string OtherTenantEncf = "E310000000004";
    private const string IssuerTaxId = "130862346";
    private const string TimbreUrl =
        "https://ecf.dgii.gov.do/testecf/consultatimbre?rncemisor=130862346&rnccomprador=130862354&encf=E310000000001&fechaemision=14-11-2026&montototal=236.00&fechafirma=14-11-2026%2010:30:00&codigoseguridad=BoXjC8";

    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    private static readonly object SeedLock = new();
    private static bool _seeded;
    private static string _signedXml = string.Empty;

    public ElectronicInvoicePrintEndpointTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _factory.EnsureSchemaCreated();

        lock (SeedLock)
        {
            if (!_seeded)
            {
                SeedAsync().GetAwaiter().GetResult();
                _seeded = true;
            }
        }
    }

    [Fact]
    public async Task TheEndpointPrintsTheRepresentationOfASignedDocument()
    {
        SetAuth(Guid7TestValues.CompanyA);

        var response = await _client.GetAsync($"/api/ElectronicInvoicePrint/{SignedInvoiceId}");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/html");

        body.Should().Contain(SignedEncf, "the representation carries the e-NCF of the document");
        body.Should().Contain(IssuerTaxId, "the representation carries the RNC of the issuer");
        body.Should().Contain("Fecha Vencimiento: 31-12-2028",
            "the expiry of the sequence comes from the signed document");
        body.Should().Contain("Comprador SRL", "the full layout names the buyer");
        body.Should().Contain("Café molido", "the line description is NombreItem");
        body.Should().NotContain("Descripcion interna no oficial");
        body.Should().Contain("Código de Seguridad");
        body.Should().Contain(SecurityCode(), "the security code comes out of the signature");
        body.Should().Contain(TimbreUrl.Replace("&", "&amp;"), "the QR of the representation opens the timbre of the document");
        body.Should().Contain("<svg", "the QR travels inside the representation");
    }

    [Fact]
    public async Task ThePrintedQrSymbolIsAtLeastTwentyTwoMillimeters()
    {
        SetAuth(Guid7TestValues.CompanyA);

        var response = await _client.GetAsync($"/api/ElectronicInvoicePrint/{SignedInvoiceId}");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var css = Regex.Match(
            body,
            @"\.qr \{ width: (?<width>[\d.]+)mm; height: (?<height>[\d.]+)mm; padding: (?<padding>[\d.]+)mm; \}");
        var width = decimal.Parse(css.Groups["width"].Value, CultureInfo.InvariantCulture);
        var padding = decimal.Parse(css.Groups["padding"].Value, CultureInfo.InvariantCulture);

        var start = body.IndexOf("<svg", StringComparison.Ordinal);
        var end = body.IndexOf("</svg>", start, StringComparison.Ordinal) + "</svg>".Length;
        var svg = XDocument.Parse(body[start..end]);
        var viewBox = svg.Root!.Attribute("viewBox")!.Value.Split(' ');
        var viewBoxWidth = decimal.Parse(viewBox[2], CultureInfo.InvariantCulture);
        var quietZoneWidth = 4m;
        var symbolWidth = viewBoxWidth - (2m * quietZoneWidth);
        var printedSymbolWidth = (width - (2m * padding)) * symbolWidth / viewBoxWidth;

        printedSymbolWidth.Should().BeGreaterThanOrEqualTo(22m);
        body.Should().Contain("@page { size: letter; margin: 20mm; }");
        body.Should().Contain("footer { position: absolute; bottom: 0; left: 0; right: 0;");
    }

    [Fact]
    public async Task TheRepresentationShowsTheIssuerOfTheSignedDocumentAndNotTheOneOfTheRow()
    {
        SetAuth(Guid7TestValues.CompanyA);

        var response = await _client.GetAsync($"/api/ElectronicInvoicePrint/{SignedInvoiceId}");
        var body = await response.Content.ReadAsStringAsync();

        // The representation is built from the document that was signed: the
        // commercial data of the company in the row is not the one the DGII
        // received.
        body.Should().Contain("Company A Legal");
        body.Should().Contain("Company A Commercial Name");
        body.Should().Contain("Sucursal Principal");
        body.Should().Contain("Signed Issuer Address");
        body.Should().Contain("010100");
        body.Should().Contain("010000");
        body.Should().NotContain(LiveNameOfTheCompany);
        body.Should().NotContain("1 A Street");
    }

    [Fact]
    public async Task TheTotalsComeFromTheSignedDocumentAndNotFromTheMutableRow()
    {
        SetAuth(Guid7TestValues.CompanyA);

        var response = await _client.GetAsync($"/api/ElectronicInvoicePrint/{SignedInvoiceId}");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain("<td>Subtotal Gravado</td><td class=\"amount\">200.00</td>");
        body.Should().Contain("<td>Total ITBIS</td><td class=\"amount\">36.00</td>");
        body.Should().Contain("<tr><th>Total</th><th class=\"amount\">236.00</th></tr>");
        body.Should().NotContain("999.00");
        body.Should().NotContain("888.00");
        body.Should().NotContain("777.00");
    }

    [Fact]
    public async Task TheRepresentationOfAConsumerFinalConsumerUsesTheSimplifiedLayout()
    {
        SetAuth(Guid7TestValues.CompanyA);

        var response = await _client.GetAsync($"/api/ElectronicInvoicePrint/{ConsumerInvoiceId}");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain("data-template=\"final-consumer\"");
        body.Should().NotContain("<div class=\"buyer\">", "the simplified layout has no buyer block");
    }

    [Fact]
    public async Task TheEndpointDoesNotPrintADocumentThatIsNotSigned()
    {
        SetAuth(Guid7TestValues.CompanyA);

        var response = await _client.GetAsync($"/api/ElectronicInvoicePrint/{DraftInvoiceId}");
        var body = await response.Content.ReadAsStringAsync();

        // The security code comes out of the signature: a document without one
        // has no printed representation.
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Should().Contain("not signed");
        body.Should().NotContain("<html");
    }

    [Fact]
    public async Task TheEndpointDoesNotPrintADocumentTheDgiiRejected()
    {
        SetAuth(Guid7TestValues.CompanyA);

        var response = await _client.GetAsync($"/api/ElectronicInvoicePrint/{RejectedInvoiceId}");
        var body = await response.Content.ReadAsStringAsync();

        // A rejected document has no fiscal credit behind it, so its printed
        // representation cannot support one.
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Should().Contain("rejected");
        body.Should().NotContain("<html");
    }

    [Fact]
    public async Task TheEndpointDoesNotPrintADocumentTheDgiiHasNotAccepted()
    {
        SetAuth(Guid7TestValues.CompanyA);

        var response = await _client.GetAsync($"/api/ElectronicInvoicePrint/{InProcessInvoiceId}");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Should().Contain("printable");
        body.Should().NotContain("<html");
    }

    [Fact]
    public async Task TheEndpointDoesNotPrintAnAnnulledDocument()
    {
        SetAuth(Guid7TestValues.CompanyA);

        var response = await _client.GetAsync($"/api/ElectronicInvoicePrint/{AnnulledInvoiceId}");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Should().Contain("annulled");
        body.Should().NotContain("<html");
    }

    [Fact]
    public async Task TheDocumentOfAnotherCompanyIsNotPrinted()
    {
        SetAuth(Guid7TestValues.CompanyA);

        var response = await _client.GetAsync($"/api/ElectronicInvoicePrint/{OtherTenantInvoiceId}");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Should().NotContain(OtherTenantEncf, "the document of another company never comes out");
    }

    private static Guid SignedInvoiceId { get; } = Guid.CreateVersion7();
    private static Guid ConsumerInvoiceId { get; } = Guid.CreateVersion7();
    private static Guid DraftInvoiceId { get; } = Guid.CreateVersion7();
    private static Guid InProcessInvoiceId { get; } = Guid.CreateVersion7();
    private static Guid RejectedInvoiceId { get; } = Guid.CreateVersion7();
    private static Guid AnnulledInvoiceId { get; } = Guid.CreateVersion7();
    private static Guid OtherTenantInvoiceId { get; } = Guid.CreateVersion7();

    // The name the company has today in its row. It is not the one the
    // signed document carries.
    private const string LiveNameOfTheCompany = "El Nombre De La Empresa Hoy";

    // The security code of the document the seed signs: the first six
    // characters of its signature.
    private static string SecurityCode() => EcfSecurityCode.From(_signedXml);

    private void SetAuth(Guid companyId)
    {
        var token = JwtTestHelper.MintToken(_factory, companyId, Guid7TestValues.UserA);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VolterpDbContext>();

        db.Companies.AddRange(
            new Company
            {
                Id = Guid7TestValues.CompanyA,
                Name = "Company A",
                TaxId = IssuerTaxId,
                Address = "1 A Street",
                LegalName = LiveNameOfTheCompany,
                Email = "a@company.test",
                Phone = "555-0001",
                IsActive = true
            },
            new Company
            {
                Id = Guid7TestValues.CompanyB,
                Name = "Company B",
                TaxId = "130862354",
                Address = "2 B Street",
                LegalName = "Company B Legal",
                Email = "b@company.test",
                Phone = "555-0002",
                IsActive = true
            });

        var saleId = Guid.CreateVersion7();
        var consumerSaleId = Guid.CreateVersion7();
        var draftSaleId = Guid.CreateVersion7();
        var inProcessSaleId = Guid.CreateVersion7();
        var rejectedSaleId = Guid.CreateVersion7();
        var annulledSaleId = Guid.CreateVersion7();
        var otherSaleId = Guid.CreateVersion7();

        // One sale per document: a sale carries one e-CF.
        db.Sales.AddRange(
            new Sale
            {
                Id = saleId,
                CompanyId = Guid7TestValues.CompanyA,
                Code = "V-0101",
                Status = SaleStatus.Completed,
                DocumentType = EcfDocumentType.FiscalCredit,
                ReceptorTaxId = "130862354",
                Subtotal = 200m,
                Itbis = 36m,
                Total = 236m
            },
            new Sale
            {
                Id = consumerSaleId,
                CompanyId = Guid7TestValues.CompanyA,
                Code = "V-0102",
                Status = SaleStatus.Completed,
                DocumentType = EcfDocumentType.Consumer,
                Subtotal = 200m,
                Itbis = 36m,
                Total = 236m
            },
            new Sale
            {
                Id = draftSaleId,
                CompanyId = Guid7TestValues.CompanyA,
                Code = "V-0103",
                Status = SaleStatus.Completed,
                DocumentType = EcfDocumentType.Consumer,
                Subtotal = 200m,
                Itbis = 36m,
                Total = 236m
            },
            new Sale
            {
                Id = inProcessSaleId,
                CompanyId = Guid7TestValues.CompanyA,
                Code = "V-0106",
                Status = SaleStatus.Completed,
                DocumentType = EcfDocumentType.FiscalCredit,
                ReceptorTaxId = "130862354",
                Subtotal = 200m,
                Itbis = 36m,
                Total = 236m
            },
            new Sale
            {
                Id = rejectedSaleId,
                CompanyId = Guid7TestValues.CompanyA,
                Code = "V-0105",
                Status = SaleStatus.Completed,
                DocumentType = EcfDocumentType.FiscalCredit,
                ReceptorTaxId = "130862354",
                Subtotal = 200m,
                Itbis = 36m,
                Total = 236m
            },
            new Sale
            {
                Id = annulledSaleId,
                CompanyId = Guid7TestValues.CompanyA,
                Code = "V-0107",
                Status = SaleStatus.Completed,
                DocumentType = EcfDocumentType.FiscalCredit,
                ReceptorTaxId = "130862354",
                Subtotal = 200m,
                Itbis = 36m,
                Total = 236m
            },
            new Sale
            {
                Id = otherSaleId,
                CompanyId = Guid7TestValues.CompanyB,
                Code = "V-0104",
                Status = SaleStatus.Completed,
                DocumentType = EcfDocumentType.FiscalCredit,
                ReceptorTaxId = "130862354",
                Subtotal = 200m,
                Itbis = 36m,
                Total = 236m
            });

        var signedXml = SignedXmlOf(BuyerTaxId: "130862354");
        _signedXml = signedXml;

        db.ElectronicInvoices.AddRange(
            new ElectronicInvoice
            {
                Id = SignedInvoiceId,
                CompanyId = Guid7TestValues.CompanyA,
                SaleId = saleId,
                ENCF = SignedEncf,
                DocumentType = EcfDocumentType.FiscalCredit,
                IssuerTaxId = IssuerTaxId,
                ReceiverTaxId = "130862354",
                SignedXml = signedXml,
                SecurityCode = SecurityCode(),
                QrUrl = TimbreUrl,
                SignedAt = new DateTime(2026, 11, 14, 10, 30, 0),
                Status = EcfStatus.Accepted,
                // Deliberately different from the signed document: the
                // representation must use the signed totals.
                Subtotal = 999m,
                Itbis = 888m,
                Total = 777m
            },
            new ElectronicInvoice
            {
                Id = ConsumerInvoiceId,
                CompanyId = Guid7TestValues.CompanyA,
                SaleId = consumerSaleId,
                ENCF = ConsumerEncf,
                DocumentType = EcfDocumentType.Consumer,
                IssuerTaxId = IssuerTaxId,
                SignedXml = SignedXmlOf(BuyerTaxId: null),
                SecurityCode = SecurityCode(),
                QrUrl = TimbreUrl,
                SignedAt = new DateTime(2026, 11, 14, 10, 30, 0),
                Status = EcfStatus.Accepted,
                Subtotal = 200m,
                Itbis = 36m,
                Total = 236m
            },
            new ElectronicInvoice
            {
                Id = DraftInvoiceId,
                CompanyId = Guid7TestValues.CompanyA,
                SaleId = draftSaleId,
                ENCF = DraftEncf,
                DocumentType = EcfDocumentType.Consumer,
                IssuerTaxId = IssuerTaxId,
                Status = EcfStatus.Draft,
                Subtotal = 200m,
                Itbis = 36m,
                Total = 236m
            },
            new ElectronicInvoice
            {
                Id = InProcessInvoiceId,
                CompanyId = Guid7TestValues.CompanyA,
                SaleId = inProcessSaleId,
                ENCF = InProcessEncf,
                DocumentType = EcfDocumentType.FiscalCredit,
                IssuerTaxId = IssuerTaxId,
                ReceiverTaxId = "130862354",
                SignedXml = signedXml,
                SecurityCode = SecurityCode(),
                QrUrl = TimbreUrl,
                SignedAt = new DateTime(2026, 11, 14, 10, 30, 0),
                Status = EcfStatus.InProcess,
                Subtotal = 200m,
                Itbis = 36m,
                Total = 236m
            },
            new ElectronicInvoice
            {
                Id = RejectedInvoiceId,
                CompanyId = Guid7TestValues.CompanyA,
                SaleId = rejectedSaleId,
                ENCF = RejectedEncf,
                DocumentType = EcfDocumentType.FiscalCredit,
                IssuerTaxId = IssuerTaxId,
                ReceiverTaxId = "130862354",
                SignedXml = signedXml,
                SecurityCode = SecurityCode(),
                QrUrl = TimbreUrl,
                SignedAt = new DateTime(2026, 11, 14, 10, 30, 0),
                Status = EcfStatus.Rejected,
                VerdictNote = "El RNC del receptor no existe.",
                Subtotal = 200m,
                Itbis = 36m,
                Total = 236m
            },
            new ElectronicInvoice
            {
                Id = AnnulledInvoiceId,
                CompanyId = Guid7TestValues.CompanyA,
                SaleId = annulledSaleId,
                ENCF = AnnulledEncf,
                DocumentType = EcfDocumentType.FiscalCredit,
                IssuerTaxId = IssuerTaxId,
                ReceiverTaxId = "130862354",
                SignedXml = signedXml,
                SecurityCode = SecurityCode(),
                QrUrl = TimbreUrl,
                SignedAt = new DateTime(2026, 11, 14, 10, 30, 0),
                Status = EcfStatus.Annulled,
                Subtotal = 200m,
                Itbis = 36m,
                Total = 236m
            },
            new ElectronicInvoice
            {
                Id = OtherTenantInvoiceId,
                CompanyId = Guid7TestValues.CompanyB,
                SaleId = otherSaleId,
                ENCF = OtherTenantEncf,
                DocumentType = EcfDocumentType.FiscalCredit,
                IssuerTaxId = "130862354",
                SignedXml = signedXml,
                SecurityCode = SecurityCode(),
                QrUrl = TimbreUrl,
                SignedAt = new DateTime(2026, 11, 14, 10, 30, 0),
                Status = EcfStatus.Signed,
                Subtotal = 200m,
                Itbis = 36m,
                Total = 236m
            });

        await db.SaveChangesAsync();
    }

    // The document the seed signs: the writer produces the XML and the
    // signer signs it, so the representation is built from a real signature.
    private static string SignedXmlOf(string? BuyerTaxId)
    {
        var items = new[]
        {
            new EcfItem
            {
                LineNumber = 1,
                TaxTreatment = TaxCode.ItbisTasa1,
                Name = "Café molido",
                Description = "Descripcion interna no oficial",
                Kind = EcfItemKind.Goods,
                Quantity = 2.5m,
                UnitPrice = 80m,
                Amount = 200m
            }
        };

        var document = new EcfDocument
        {
            DocumentType = BuyerTaxId is null ? EcfDocumentType.Consumer : EcfDocumentType.FiscalCredit,
            Identity = new EcfIdentity
            {
                ENCF = BuyerTaxId is null ? ConsumerEncf : SignedEncf,
                SequenceExpiresAt = BuyerTaxId is null ? null : new DateTime(2028, 12, 31),
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
            Buyer = BuyerTaxId is null
                ? new EcfBuyer { BusinessName = "Consumidor Final" }
                : new EcfBuyer { TaxId = BuyerTaxId, BusinessName = "Comprador SRL" },
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
}
