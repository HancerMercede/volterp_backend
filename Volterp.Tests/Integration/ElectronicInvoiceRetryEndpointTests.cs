using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.ElectronicInvoicing;
using Volterp.Infrastructure.Data;
using Volterp.Tests.TestHelpers;
using Xunit;

namespace Volterp.Tests.Integration;

[Collection("VolterpApiFactory")]
public class ElectronicInvoiceRetryEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    private static readonly object SeedLock = new();
    private static bool _seeded;
    private static Guid _otherTenantInvoiceId;
    private static Guid _notWaitingInvoiceId;
    private static Guid _waitingInvoiceId;

    public ElectronicInvoiceRetryEndpointTests(ApiFactory factory)
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
    public async Task RetryOfADocumentOfAnotherTenantIsNotFound()
    {
        SetAuth(Guid7TestValues.CompanyA);

        var response = await _client.PostAsync(
            $"/api/ElectronicInvoices/{_otherTenantInvoiceId}/retry",
            null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RetryOfADocumentThatDoesNotWaitForAReviewIsBadRequest()
    {
        SetAuth(Guid7TestValues.CompanyA);

        var response = await _client.PostAsync(
            $"/api/ElectronicInvoices/{_notWaitingInvoiceId}/retry",
            null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("does not wait for a review");
    }

    [Fact]
    public async Task RetryThatCannotSignLeavesTheDocumentWaitingForReview()
    {
        SetAuth(Guid7TestValues.CompanyA);

        var response = await _client.PostAsync(
            $"/api/ElectronicInvoices/{_waitingInvoiceId}/retry",
            null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("no active signing certificate");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VolterpDbContext>();
        var invoice = await db.ElectronicInvoices
            .IgnoreQueryFilters()
            .SingleAsync(i => i.Id == _waitingInvoiceId);

        invoice.Status.Should().Be(EcfStatus.Draft);
        invoice.RequiresReview.Should().BeTrue();
        invoice.SignedXml.Should().BeEmpty();
        invoice.VerdictNote.Should().Contain("no active signing certificate");
    }

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
                TaxId = "130862346",
                Address = "1 A Street",
                LegalName = "Company A Legal",
                Email = "a@company.test",
                Phone = "809-555-1234",
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
                Phone = "809-555-1235",
                IsActive = true
            });

        var otherTenantSaleId = Guid.CreateVersion7();
        var notWaitingSaleId = Guid.CreateVersion7();
        var waitingSaleId = Guid.CreateVersion7();

        var otherTenantSale = Sale(otherTenantSaleId, Guid7TestValues.CompanyB, "V-0001");
        var notWaitingSale = Sale(notWaitingSaleId, Guid7TestValues.CompanyA, "V-0002");
        var waitingSale = Sale(waitingSaleId, Guid7TestValues.CompanyA, "V-0003");

        db.Sales.AddRange(otherTenantSale, notWaitingSale, waitingSale);

        _otherTenantInvoiceId = Guid.CreateVersion7();
        _notWaitingInvoiceId = Guid.CreateVersion7();
        _waitingInvoiceId = Guid.CreateVersion7();

        db.ElectronicInvoices.AddRange(
            new ElectronicInvoice
            {
                Id = _otherTenantInvoiceId,
                CompanyId = Guid7TestValues.CompanyB,
                SaleId = otherTenantSaleId,
                ENCF = "E320000000003",
                DocumentType = EcfDocumentType.Consumer,
                IssuerTaxId = "130862354",
                Total = 236m,
                Status = EcfStatus.Draft,
                RequiresReview = true
            },
            new ElectronicInvoice
            {
                Id = _notWaitingInvoiceId,
                CompanyId = Guid7TestValues.CompanyA,
                SaleId = notWaitingSaleId,
                ENCF = "E320000000002",
                DocumentType = EcfDocumentType.Consumer,
                IssuerTaxId = "130862346",
                Total = 236m,
                Status = EcfStatus.Accepted
            },
            new ElectronicInvoice
            {
                Id = _waitingInvoiceId,
                CompanyId = Guid7TestValues.CompanyA,
                SaleId = waitingSaleId,
                ENCF = "E320000000001",
                DocumentType = EcfDocumentType.Consumer,
                IssuerTaxId = "130862346",
                Total = 236m,
                Status = EcfStatus.Draft,
                RequiresReview = true,
                VerdictNote = "The previous signature attempt failed."
            });

        db.ElectronicInvoiceSequences.AddRange(
            new ElectronicInvoiceSequence
            {
                Id = Guid.CreateVersion7(),
                CompanyId = Guid7TestValues.CompanyA,
                IssuerTaxId = "130862346",
                DocumentType = EcfDocumentType.Consumer,
                From = 1,
                To = 50_000_000,
                CurrentSequence = 2
            },
            new ElectronicInvoiceSequence
            {
                Id = Guid.CreateVersion7(),
                CompanyId = Guid7TestValues.CompanyB,
                IssuerTaxId = "130862354",
                DocumentType = EcfDocumentType.Consumer,
                From = 1,
                To = 50_000_000,
                CurrentSequence = 3
            });

        await db.SaveChangesAsync();
    }

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
}
