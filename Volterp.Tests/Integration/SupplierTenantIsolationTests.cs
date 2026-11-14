using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Volterp.Domain.Entities;
using Volterp.Infrastructure.Data;
using Volterp.Tests.TestHelpers;
using Xunit;

namespace Volterp.Tests.Integration;

[Collection("VolterpApiFactory")]
public class SupplierTenantIsolationTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    private static readonly object SeedLock = new();
    private static bool _seeded;

    public SupplierTenantIsolationTests(ApiFactory factory)
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

    private async Task SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VolterpDbContext>();

        db.Companies.AddRange(
            new Company
            {
                Id = Guid7TestValues.CompanyA,
                Name = "Company A",
                TaxId = "TA000000001",
                Address = "1 A Street",
                LegalName = "Company A Legal",
                Email = "a@company.test",
                Phone = "555-0001",
                IsActive = true
            },
            new Company
            {
                Id = Guid7TestValues.CompanyB,
                Name = "Company B",
                TaxId = "TB000000002",
                Address = "2 B Street",
                LegalName = "Company B Legal",
                Email = "b@company.test",
                Phone = "555-0002",
                IsActive = true
            });

        var supplierBId = Guid.Parse("0192b9c7-b6a3-7def-8c5e-1f2a3b4c5d80");
        db.Suppliers.AddRange(
            new Supplier
            {
                Id = Guid7TestValues.SupplierA,
                CompanyId = Guid7TestValues.CompanyA,
                Name = "Supplier A",
                Email = "a@supplier.test",
                Phone = "555-0001",
                Address = "1 A Street",
                Category = "Wholesale",
                ContactPerson = "Alice",
                IsActive = true
            },
            new Supplier
            {
                Id = supplierBId,
                CompanyId = Guid7TestValues.CompanyB,
                Name = "Supplier B",
                Email = "b@supplier.test",
                Phone = "555-0002",
                Address = "2 B Street",
                Category = "Retail",
                ContactPerson = "Bob",
                IsActive = true
            });

        await db.SaveChangesAsync();
    }

    private void SetAuth(Guid companyId, Guid userId, string role = "Admin")
    {
        var token = JwtTestHelper.MintToken(_factory, companyId, userId, role);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    [Fact]
    public async Task SupplierFromOtherTenant_IsBlockedByHasQueryFilter()
    {
        SetAuth(Guid7TestValues.CompanyB, Guid.NewGuid());

        var response = await _client.GetAsync("/api/suppliers");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Supplier B");
        body.Should().NotContain("Supplier A",
            "HasQueryFilter must block cross-tenant reads: Company A's supplier must NOT be visible to Company B.");
    }

    [Fact]
    public async Task CrossTenantGetById_IsBlockedByHasQueryFilter()
    {
        SetAuth(Guid7TestValues.CompanyB, Guid.NewGuid());

        var response = await _client.GetAsync($"/api/suppliers/{Guid7TestValues.SupplierA}");

        ((int)response.StatusCode).Should().BeOneOf(new[] { (int)HttpStatusCode.NotFound, (int)HttpStatusCode.NoContent },
            "Company B cannot fetch Company A's supplier by id — the query filter hides it. The service returns no DTO and the controller surfaces that as 404 (NotFound) or 204 (Ok(null)) depending on how the Either unwraps a null Right.");
    }

    [Fact]
    public async Task NoCompanyClaim_ReturnsUnauthorized()
    {
        var token = MintTokenWithoutCompanyClaim();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/suppliers");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private string MintTokenWithoutCompanyClaim()
    {
        var config = _factory.Services.GetRequiredService<IConfiguration>();
        var key = config["Jwt:Key"]!;
        var issuer = config["Jwt:Issuer"]!;
        var audience = config["Jwt:Audience"]!;
        var expiryMinutes = int.Parse(config["Jwt:ExpiryMinutes"]!);

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "no-company-user"),
            new Claim(ClaimTypes.Email, "nocompany@test.local"),
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}