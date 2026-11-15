using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using EitherWay;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Domain.Entities;
using Volterp.Infrastructure.Data;
using Volterp.Infrastructure.UnitOfWork;
using Volterp.Tests.Integration;
using Volterp.Tools.Seeding;
using Xunit;

namespace Volterp.Tests.Seeding;

[Collection("VolterpApiFactory")]
public class DemoInvoiceEndpointTests : IClassFixture<ApiFactory>
{
    private const string FirstTaxId = "101500018";
    private const string SecondTaxId = "101500026";

    private readonly ApiFactory _factory;

    public DemoInvoiceEndpointTests(ApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureSchemaCreated();
    }

    [Fact]
    public async Task TheEndpointCountsOneInvoiceInEachOpenState()
    {
        var companyId = Guid.CreateVersion7();

        await SeedAsync(companyId, FirstTaxId, createCompany: true);

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            JwtTestHelper.MintToken(_factory, companyId, Guid.CreateVersion7()));

        var response = await client.GetAsync("/api/ElectronicInvoices/state");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        var counts = root.GetProperty("counts");

        counts.GetProperty("pendingEmission").GetInt32().Should().Be(1);
        counts.GetProperty("inProcess").GetInt32().Should().Be(1);
        counts.GetProperty("rejected").GetInt32().Should().Be(1);
        counts.GetProperty("requiresReview").GetInt32().Should().Be(1);
        counts.GetProperty("total").GetInt32().Should().Be(4);

        root.GetProperty("invoices").EnumerateArray()
            .Select(invoice => invoice.GetProperty("fiscalState").GetString() ?? string.Empty)
            .OrderBy(state => state)
            .Should().Equal(
                new[] { "InProcess", "PendingEmission", "Rejected", "RequiresReview" },
                "the front has one case for every open fiscal state");
    }

    [Fact]
    public async Task RunningTheSeederTwiceDoesNotDuplicateTheInvoices()
    {
        var companyId = Guid.CreateVersion7();

        await SeedAsync(companyId, SecondTaxId, createCompany: true);
        await SeedAsync(companyId, SecondTaxId, createCompany: false);

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            JwtTestHelper.MintToken(_factory, companyId, Guid.CreateVersion7()));

        var response = await client.GetAsync("/api/ElectronicInvoices/state");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var counts = document.RootElement.GetProperty("counts");

        counts.GetProperty("total").GetInt32().Should().Be(4,
            "the second run finds the invoices of the tenant instead of writing them again");
    }

    private async Task SeedAsync(Guid companyId, string taxId, bool createCompany)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<VolterpDbContext>();

        if (createCompany)
        {
            context.Companies.Add(new Company
            {
                Id = companyId,
                Name = "Distribuidora del Caribe",
                TaxId = taxId,
                Address = "Address",
                LegalName = "Distribuidora del Caribe",
                Phone = "8090000000",
                Email = "demo@volterp.test"
            });

            await context.SaveChangesAsync();
        }

        var result = await new DemoInvoiceSeeder(new UnitOfWork(context), context)
            .SeedAsync(taxId);

        result.Should().BeOfType<Either<Error, DemoInvoiceSeedOutcome>.Right>();
    }
}
