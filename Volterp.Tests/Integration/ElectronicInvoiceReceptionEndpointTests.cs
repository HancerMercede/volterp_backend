using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Volterp.Api.Authentication;
using Volterp.Api.Controllers;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.ElectronicInvoicing;
using Volterp.Infrastructure.Data;
using Volterp.Tests.Authentication;
using Volterp.Tests.Services;
using Volterp.Tests.TestHelpers;
using Xunit;

namespace Volterp.Tests.Integration;

[Collection("VolterpApiFactory")]
public class ElectronicInvoiceReceptionEndpointTests : IClassFixture<ApiFactory>
{
    private const string IssuerRnc = "130862346";
    private const string ReceiverRnc = "130862354";
    private const string OtherRnc = "101000001";
    private const string SecondCompanyRnc = "101500026";
    private const string Encf = "E310000000001";
    private const string BranchRnc = "101500034";
    private const string BranchEncf = "E310000000002";
    private const string ApprovalReason = "The commercial terms do not match.";

    private static readonly Guid CompanyA = Guid.CreateVersion7();
    private static readonly Guid CompanyB = Guid.CreateVersion7();
    private static readonly Guid ReceivedId = Guid.CreateVersion7();

    private readonly ApiFactory _factory;

    public ElectronicInvoiceReceptionEndpointTests(ApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureSchemaCreated();
    }

    [Fact]
    public async Task ReceptionEndpoint_WithoutClientCertificate_ReturnsUnauthorized()
    {
        await SeedCompaniesAsync(_factory);
        var client = _factory.CreateClient();

        var response = await client.PostAsync(
            "/fe/recepcion/api/ecf",
            ReceptionContent(
                ReceptionTestDocuments.ReceivedEcf(IssuerRnc, ReceiverRnc, Encf),
                ReceiverRnc,
                Encf));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CommercialApprovalEndpoint_WithoutClientCertificate_ReturnsUnauthorized()
    {
        await SeedCompaniesAsync(_factory);
        var client = _factory.CreateClient();

        var response = await client.PostAsync(
            "/fe/aprobacioncomercial/api/ecf",
            ReceptionContent(
                ReceptionTestDocuments.CommercialApproval(
                    IssuerRnc,
                    ReceiverRnc,
                    Encf,
                    approved: false,
                    ApprovalReason),
                ReceiverRnc,
                Encf));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ReceptionEndpoint_WithUnknownReceiver_ReturnsBadRequest()
    {
        await using var factory = await AuthenticatedFactoryAsync();
        var client = factory.CreateClient();

        var response = await client.PostAsync(
            "/fe/recepcion/api/ecf",
            ReceptionContent(
                ReceptionTestDocuments.ReceivedEcf(IssuerRnc, OtherRnc, Encf),
                OtherRnc,
                Encf));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ReceptionEndpoint_WithMalformedXml_ReturnsBadRequest()
    {
        await using var factory = await AuthenticatedFactoryAsync();
        var client = factory.CreateClient();

        var response = await client.PostAsync(
            "/fe/recepcion/api/ecf",
            ReceptionContent("<ECF><Encabezado>", ReceiverRnc, Encf));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ReceptionEndpoint_AttributesTheTenantFromTheReceiverAndIgnoresRequestTenant()
    {
        await using var factory = await AuthenticatedFactoryAsync();
        var client = factory.CreateClient();
        var content = ReceptionContent(
            ReceptionTestDocuments.ReceivedEcf(IssuerRnc, ReceiverRnc, Encf),
            ReceiverRnc,
            Encf);
        content.Add(new StringContent(CompanyB.ToString()), "companyId");

        var response = await client.PostAsync(
            $"/fe/recepcion/api/ecf?companyId={CompanyB}",
            content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        root.GetProperty("issuerTaxId").GetString().Should().Be(IssuerRnc);
        root.GetProperty("receiverTaxId").GetString().Should().Be(ReceiverRnc);
        root.TryGetProperty("companyId", out _).Should().BeFalse(
            "the public receipt does not expose the internal tenant identifier");
    }

    [Fact]
    public async Task ReceptionEndpoint_TwiceWithTheSameIssuerAndEncf_DoesNotDuplicate()
    {
        await using var factory = await AuthenticatedFactoryAsync();
        var client = factory.CreateClient();
        var xml = ReceptionTestDocuments.ReceivedEcf(IssuerRnc, ReceiverRnc, Encf);

        var first = await client.PostAsync(
            "/fe/recepcion/api/ecf",
            ReceptionContent(xml, ReceiverRnc, Encf));
        var second = await client.PostAsync(
            "/fe/recepcion/api/ecf",
            ReceptionContent(xml, ReceiverRnc, Encf));

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("alreadyReceived").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task ReceptionEndpoint_WhenTheNaturalKeyBelongsToAnotherTenant_ReturnsGenericConflictWithoutChangingStoredData()
    {
        await using var factory = await AuthenticatedFactoryAsync();
        var client = factory.CreateClient();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VolterpDbContext>();
            db.ReceivedElectronicInvoices.Add(new ReceivedElectronicInvoice
            {
                Id = Guid.CreateVersion7(),
                CompanyId = CompanyB,
                IssuerTaxId = IssuerRnc,
                ReceiverTaxId = ReceiverRnc,
                ENCF = Encf,
                DocumentType = EcfDocumentType.FiscalCredit,
                SignedXml = ReceptionTestDocuments.ReceivedEcf(IssuerRnc, ReceiverRnc, Encf),
                SignedAt = new DateTime(2026, 11, 15, 10, 30, 0, DateTimeKind.Utc),
                ReceivedAt = new DateTime(2026, 11, 15, 10, 31, 0, DateTimeKind.Utc)
            });
            await db.SaveChangesAsync();
        }

        var response = await client.PostAsync(
            "/fe/recepcion/api/ecf",
            ReceptionContent(
                ReceptionTestDocuments.ReceivedEcf(IssuerRnc, ReceiverRnc, Encf),
                ReceiverRnc,
                Encf));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("error").GetString()
            .Should().Be("The e-CF was already received.");

        using var verificationScope = factory.Services.CreateScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<VolterpDbContext>();
        var stored = await verificationDb.ReceivedElectronicInvoices
            .IgnoreQueryFilters()
            .SingleAsync();
        stored.CompanyId.Should().Be(CompanyB);
        stored.IssuerTaxId.Should().Be(IssuerRnc);
        stored.ENCF.Should().Be(Encf);
    }

    [Fact]
    public async Task CommercialApprovalEndpoint_RecordsWhoWhenAndWhy()
    {
        await using var factory = await AuthenticatedFactoryAsync();
        var client = factory.CreateClient();

        var reception = await client.PostAsync(
            "/fe/recepcion/api/ecf",
            ReceptionContent(
                ReceptionTestDocuments.ReceivedEcf(IssuerRnc, ReceiverRnc, Encf),
                ReceiverRnc,
                Encf));
        reception.StatusCode.Should().Be(HttpStatusCode.OK);

        var approval = await client.PostAsync(
            "/fe/aprobacioncomercial/api/ecf",
            ReceptionContent(
                ReceptionTestDocuments.CommercialApproval(
                    IssuerRnc,
                    ReceiverRnc,
                    Encf,
                    approved: false,
                    ApprovalReason),
                ReceiverRnc,
                Encf));

        approval.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await approval.Content.ReadAsStringAsync());
        var root = document.RootElement;
        root.GetProperty("approvalStatus").GetString().Should().Be("Rejected");
        root.GetProperty("approvalDecidedBy").GetString().Should().Be(ReceiverRnc);
        root.GetProperty("approvalDecidedAt").GetString().Should().NotBeNullOrWhiteSpace();
        root.GetProperty("approvalReason").GetString().Should().Be(ApprovalReason);
    }

    [Fact]
    public async Task TheRoutesThatAlreadyExisted_KeepAnsweringTheSame()
    {
        await using var factory = await AuthenticatedFactoryAsync();
        var client = factory.CreateClient();

        var reception = await client.PostAsync(
            "/fe/recepcion/api/ecf",
            ReceptionContent(
                ReceptionTestDocuments.ReceivedEcf(IssuerRnc, ReceiverRnc, Encf),
                ReceiverRnc,
                Encf));

        reception.StatusCode.Should().Be(HttpStatusCode.OK);

        using var received = JsonDocument.Parse(await reception.Content.ReadAsStringAsync());
        var receivedRoot = received.RootElement;
        receivedRoot.GetProperty("issuerTaxId").GetString().Should().Be(IssuerRnc);
        receivedRoot.GetProperty("receiverTaxId").GetString().Should().Be(ReceiverRnc);
        receivedRoot.GetProperty("encf").GetString().Should().Be(Encf);
        receivedRoot.GetProperty("documentType").GetString().Should().Be("FiscalCredit");
        receivedRoot.GetProperty("approvalStatus").GetString().Should().Be("Received");
        receivedRoot.GetProperty("alreadyReceived").GetBoolean().Should().BeFalse();

        var approval = await client.PostAsync(
            "/fe/aprobacioncomercial/api/ecf",
            ReceptionContent(
                ReceptionTestDocuments.CommercialApproval(
                    IssuerRnc,
                    ReceiverRnc,
                    Encf,
                    approved: true,
                    ApprovalReason),
                ReceiverRnc,
                Encf));

        approval.StatusCode.Should().Be(HttpStatusCode.OK);

        using var approved = JsonDocument.Parse(await approval.Content.ReadAsStringAsync());
        approved.RootElement.GetProperty("approvalStatus").GetString().Should().Be("Approved");
        approved.RootElement.GetProperty("approvalDecidedBy").GetString().Should().Be(ReceiverRnc);
        approved.RootElement.GetProperty("approvalReason").GetString().Should().Be(ApprovalReason);
    }

    [Fact]
    public async Task TenantDecisionEndpoint_UsesTheTenantFromTheTokenAndHidesAnotherTenant()
    {
        await SeedCompaniesAsync(_factory);
        await SeedReceivedInvoiceAsync(_factory);
        var client = _factory.CreateClient();

        var tokenA = JwtTestHelper.MintToken(_factory, CompanyA, Guid7TestValues.UserA);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);

        var approved = await client.PostAsJsonAsync(
            $"/api/ElectronicInvoiceReception/{ReceivedId}/commercial-approval",
            new { approved = true, reason = "Approved by the buyer" });

        approved.StatusCode.Should().Be(HttpStatusCode.OK);
        using var approvedDocument = JsonDocument.Parse(await approved.Content.ReadAsStringAsync());
        approvedDocument.RootElement.GetProperty("approvalStatus").GetString().Should().Be("Approved");
        approvedDocument.RootElement.GetProperty("approvalDecidedBy").GetString()
            .Should().Be(Guid7TestValues.UserA.ToString());

        var tokenB = JwtTestHelper.MintToken(_factory, CompanyB, Guid7TestValues.UserA);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);

        var hidden = await client.PostAsJsonAsync(
            $"/api/ElectronicInvoiceReception/{ReceivedId}/commercial-approval",
            new { approved = false, reason = "A decision from another tenant" });

        hidden.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ReceptionEndpoint_WithTheRncOfABranch_AttributesTheDocumentToTheTenant()
    {
        await using var factory = await AuthenticatedFactoryAsync();
        var client = factory.CreateClient();

        await SeedBranchAsync(factory);

        var response = await client.PostAsync(
            "/fe/recepcion/api/ecf",
            ReceptionContent(
                ReceptionTestDocuments.ReceivedEcf(IssuerRnc, BranchRnc, BranchEncf),
                BranchRnc,
                BranchEncf));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VolterpDbContext>();

        var received = await db.ReceivedElectronicInvoices
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(invoice => invoice.ENCF == BranchEncf);

        received.CompanyId.Should().Be(CompanyA,
            "the branch belongs to the tenant: the document is attributed to it");
    }

    [Fact]
    public async Task ReceptionEndpoint_WithAnAmbiguousReceiverRnc_ReturnsBadRequest()
    {
        await using var factory = await AuthenticatedFactoryAsync();
        var client = factory.CreateClient();

        // The same RNC claimed by two tenants: the receiver is ambiguous and
        // the document cannot be attributed to one of them.
        await SeedCompetingBranchAsync(factory);

        var response = await client.PostAsync(
            "/fe/recepcion/api/ecf",
            ReceptionContent(
                ReceptionTestDocuments.ReceivedEcf(IssuerRnc, ReceiverRnc, Encf),
                ReceiverRnc,
                Encf));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static async Task SeedBranchAsync(ApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VolterpDbContext>();

        if (!await db.CompanyIssuers.AnyAsync(issuer => issuer.TaxId == BranchRnc))
        {
            db.CompanyIssuers.Add(new CompanyIssuer
            {
                Id = Guid.CreateVersion7(),
                CompanyId = CompanyA,
                TaxId = BranchRnc,
                LegalName = "Sucursal Norte",
                Status = EntityStatus.Active
            });

            await db.SaveChangesAsync();
        }
    }

    private static async Task SeedCompetingBranchAsync(ApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VolterpDbContext>();

        if (!await db.CompanyIssuers.AnyAsync(issuer => issuer.TaxId == ReceiverRnc))
        {
            db.CompanyIssuers.Add(new CompanyIssuer
            {
                Id = Guid.CreateVersion7(),
                CompanyId = CompanyB,
                TaxId = ReceiverRnc,
                LegalName = "Sucursal en conflicto",
                Status = EntityStatus.Active
            });

            await db.SaveChangesAsync();
        }
    }

    private static async Task<ApiFactory> AuthenticatedFactoryAsync()
    {
        var authority = TestCertificateAuthority.Create();
        var clientCertificate = authority.IssueClientCertificate();
        var factory = new ApiFactory(services =>
        {
            services.Configure<DgiiCertificateAuthenticationOptions>(
                ElectronicInvoiceReceptionController.DgiiAuthenticationScheme,
                options =>
                {
                    options.RevocationMode = X509RevocationMode.NoCheck;
                    options.TrustedRootCertificates.Add(authority.Root);
                });
            services.AddSingleton<IStartupFilter>(
                new ClientCertificateStartupFilter(authority.Root, clientCertificate));
        });

        factory.EnsureSchemaCreated();
        await SeedCompaniesAsync(factory);
        return factory;
    }

    private static async Task SeedCompaniesAsync(ApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VolterpDbContext>();

        if (!await db.Companies.AnyAsync(c => c.Id == CompanyA))
        {
            db.Companies.AddRange(
                Company(CompanyA, ReceiverRnc),
                Company(CompanyB, SecondCompanyRnc));
            await db.SaveChangesAsync();
        }
    }

    private static async Task SeedReceivedInvoiceAsync(ApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VolterpDbContext>();

        if (!await db.ReceivedElectronicInvoices.AnyAsync(i => i.Id == ReceivedId))
        {
            db.ReceivedElectronicInvoices.Add(new ReceivedElectronicInvoice
            {
                Id = ReceivedId,
                CompanyId = CompanyA,
                IssuerTaxId = IssuerRnc,
                ReceiverTaxId = ReceiverRnc,
                ENCF = Encf,
                DocumentType = EcfDocumentType.FiscalCredit,
                SignedXml = ReceptionTestDocuments.ReceivedEcf(IssuerRnc, ReceiverRnc, Encf),
                SignedAt = new DateTime(2026, 11, 15, 10, 30, 0, DateTimeKind.Utc),
                ReceivedAt = new DateTime(2026, 11, 15, 10, 31, 0, DateTimeKind.Utc)
            });
            await db.SaveChangesAsync();
        }
    }

    private static Company Company(Guid id, string taxId)
        => new()
        {
            Id = id,
            Name = $"Company {id:N}",
            LegalName = $"Company {id:N}",
            TaxId = taxId,
            Address = "1 Main Street",
            Email = $"company-{id:N}@test.local",
            Phone = "809-000-0000",
            IsActive = true
        };

    private static MultipartFormDataContent ReceptionContent(
        string xml,
        string receiverRnc,
        string encf)
    {
        var file = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(xml));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/xml");

        return new MultipartFormDataContent
        {
            { file, "xml", ReceptionTestDocuments.FileName(receiverRnc, encf) }
        };
    }

    private sealed class ClientCertificateStartupFilter(
        X509Certificate2 root,
        X509Certificate2 clientCertificate) : IStartupFilter, IDisposable
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
            => app =>
            {
                app.Use(async (context, nextMiddleware) =>
                {
                    context.Connection.ClientCertificate = clientCertificate;
                    await nextMiddleware();
                });
                next(app);
            };

        public void Dispose()
        {
            root.Dispose();
            clientCertificate.Dispose();
        }
    }
}
