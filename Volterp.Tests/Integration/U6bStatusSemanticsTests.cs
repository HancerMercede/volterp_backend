using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
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

// The status says what happened: a sale that does not exist is a 404, a
// sale that breaks a rule is a 400, the rate limit answers with the same
// envelope as the rest of the API and the diagnostics are not public.
[Collection("VolterpApiFactory")]
public class U6bStatusSemanticsTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public U6bStatusSemanticsTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _factory.EnsureSchemaCreated();
    }

    [Fact]
    public async Task CompletingASaleThatDoesNotExistAnswersNotFound()
    {
        SetAuth(Guid7TestValues.CompanyA);

        var response = await _client.PutAsync($"/api/Sales/{Guid.CreateVersion7()}/complete", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ErrorOfAsync(response)).Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CompletingASaleThatBreaksARuleAnswersBadRequest()
    {
        SetAuth(Guid7TestValues.CompanyA);
        var saleId = await SeedSaleWithoutItemsAsync();

        var response = await _client.PutAsync($"/api/Sales/{saleId}/complete", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorOfAsync(response)).Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task UpdatingASaleAsCompletedAnswersBadRequest()
    {
        SetAuth(Guid7TestValues.CompanyA);
        var saleId = await SeedSaleWithoutItemsAsync();

        var response = await _client.PutAsJsonAsync($"/api/Sales/{saleId}", new
        {
            clienteId = (Guid?)null,
            clienteName = "Cliente",
            status = 1,
            total = 0m,
            notes = (string?)null,
            items = Array.Empty<object>(),
            documentType = 32,
            code = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorOfAsync(response)).Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task TheRateLimitRejectionCarriesTheErrorEnvelope()
    {
        HttpResponseMessage? rejected = null;

        // The authentication policy allows five attempts per minute.
        for (var attempt = 0; attempt < 6; attempt++)
        {
            rejected?.Dispose();
            rejected = await _client.PostAsJsonAsync("/api/auth/login", new
            {
                email = "nobody@test.local",
                password = "wrong-password"
            });
        }

        rejected!.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await ErrorOfAsync(rejected)).Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task TheDiagnosticsOfTheExceptionsAreNotPublic()
    {
        var response = await _client.GetAsync("/_diagnostics/exceptions");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<Guid> SeedSaleWithoutItemsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VolterpDbContext>();

        if (!await db.Set<Company>().AnyAsync(c => c.Id == Guid7TestValues.CompanyA))
        {
            db.Set<Company>().Add(new Company
            {
                Id = Guid7TestValues.CompanyA,
                Name = "Empresa",
                TaxId = "10100000101",
                Address = "1 A Street",
                LegalName = "Empresa S.A.",
                Phone = "555-0000",
                Email = "empresa@test.local"
            });

            await db.SaveChangesAsync();
        }

        var sale = new Sale
        {
            Id = Guid.CreateVersion7(),
            CompanyId = Guid7TestValues.CompanyA,
            Code = $"TEST-{Guid.NewGuid():N}"[..20],
            Status = SaleStatus.Pending,
            DocumentType = EcfDocumentType.Consumer
        };

        db.Set<Sale>().Add(sale);
        await db.SaveChangesAsync();

        return sale.Id;
    }

    private async Task<string?> ErrorOfAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return body.RootElement.TryGetProperty("error", out var error)
            ? error.GetString()
            : null;
    }

    private void SetAuth(Guid companyId, string role = "Admin")
    {
        var token = JwtTestHelper.MintToken(_factory, companyId, Guid7TestValues.UserA, role);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
