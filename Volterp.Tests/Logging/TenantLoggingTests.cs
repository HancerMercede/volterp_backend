using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Sinks.InMemory;
using System.Net;
using System.Net.Http.Headers;
using Volterp.Tests.Integration;
using Volterp.Tests.TestHelpers;
using Xunit;

namespace Volterp.Tests.Logging;

public class TenantLoggingTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public TenantLoggingTests(ApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureSchemaCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AuthenticatedRequest_LogsContainCompanyId()
    {
        var sink = new InMemorySink();
        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Sink(sink)
            .CreateLogger();

        var token = JwtTestHelper.MintToken(_factory, Guid7TestValues.CompanyA, Guid7TestValues.UserA);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/suppliers");

        response.IsSuccessStatusCode.Should().BeTrue();

        var events = sink.LogEvents.ToList();
        events.Should().NotBeEmpty();
        events.Should().Contain(e =>
            e.Properties.ContainsKey("CompanyId") &&
            e.Properties["CompanyId"].ToString().Contains(Guid7TestValues.CompanyA.ToString("D")));
    }

    [Fact]
    public async Task AnonymousRequest_MiddlewareDoesNotThrow()
    {
        var response = await _client.GetAsync("/api/suppliers");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
