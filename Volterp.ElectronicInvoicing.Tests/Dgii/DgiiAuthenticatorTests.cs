using System.Globalization;
using System.Net;
using System.Text;
using FluentAssertions;
using Volterp.ElectronicInvoicing.Dgii;
using Volterp.ElectronicInvoicing.Signing;
using Xunit;

namespace Volterp.ElectronicInvoicing.Tests.Dgii;

// The token belongs to the issuer that signed the seed and lives an hour, so
// the round trip is paid once per issuer and not once per emission.
public class DgiiAuthenticatorTests
{
    // "Descripción Técnica Servicios DGII", p. 10: the seed is
    // <SemillaModel><valor/><fecha/></SemillaModel>.
    private const string SeedXml = "<SemillaModel><valor>abc</valor><fecha>2019-03-13T14:33:32-04:00</fecha></SemillaModel>";

    private const string IssuerRnc = "101000001";
    private const string OtherIssuerRnc = "101000002";

    private static readonly DateTimeOffset Now = new(2026, 11, 4, 17, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AuthenticateAsync_SignsTheSeedAndTradesItForAToken()
    {
        var dgii = new DgiiStub(TokenAnswer("jwt", Now.AddHours(1)));
        using var certificate = TestCertificate.Create();
        var (_, authenticator) = CreateAuthenticator(dgii, new FakeTimeProvider(Now));

        var token = await authenticator.AuthenticateAsync(IssuerRnc, certificate);

        // "Descripción Técnica Servicios DGII", p. 10: the seed comes from the
        // authentication service; p. 11: the signed seed is traded for a token.
        dgii.SeedRequests.Should().Be(1);
        dgii.TokenRequests.Should().Be(1);
        dgii.LastTokenBody.Should().Contain("<valor>abc</valor>");
        dgii.LastTokenBody.Should().Contain("Signature");
        token.Token.Should().Be("jwt");
    }

    [Fact]
    public async Task AuthenticateAsync_TwiceForTheSameIssuer_MakesOneTripToTheDgii()
    {
        var dgii = new DgiiStub(TokenAnswer("jwt", Now.AddHours(1)));
        using var certificate = TestCertificate.Create();
        var (_, authenticator) = CreateAuthenticator(dgii, new FakeTimeProvider(Now));

        var first = await authenticator.AuthenticateAsync(IssuerRnc, certificate);
        var second = await authenticator.AuthenticateAsync(IssuerRnc, certificate);

        dgii.SeedRequests.Should().Be(1);
        dgii.TokenRequests.Should().Be(1);
        second.Token.Should().Be(first.Token);
    }

    [Fact]
    public async Task AuthenticateAsync_ForTwoIssuers_TravelsToTheDgiiForEachOneAndKeepsItsOwnToken()
    {
        var dgii = new DgiiStub(
            TokenAnswer("jwt-first", Now.AddHours(1)),
            TokenAnswer("jwt-second", Now.AddHours(1)));
        using var certificate = TestCertificate.Create();
        var (client, authenticator) = CreateAuthenticator(dgii, new FakeTimeProvider(Now));

        var first = await authenticator.AuthenticateAsync(IssuerRnc, certificate);
        var second = await authenticator.AuthenticateAsync(OtherIssuerRnc, certificate);
        var firstAgain = await authenticator.AuthenticateAsync(IssuerRnc, certificate);

        // Every issuer signs its own seed, so every issuer gets its own token.
        dgii.SeedRequests.Should().Be(2);
        dgii.TokenRequests.Should().Be(2);
        second.Token.Should().NotBe(first.Token);
        firstAgain.Token.Should().Be(first.Token);

        // And the token that goes out on the wire is the one of its issuer.
        await client.SendElectronicInvoiceAsync("<e-CF />", first.Token);
        dgii.Handler.LastAuthorization.Should().Be("bearer jwt-first");

        await client.SendElectronicInvoiceAsync("<e-CF />", second.Token);
        dgii.Handler.LastAuthorization.Should().Be("bearer jwt-second");
    }

    [Fact]
    public async Task AuthenticateAsync_WhenTheTokenExpires_RenewsIt()
    {
        var clock = new FakeTimeProvider(Now);
        var dgii = new DgiiStub(
            TokenAnswer("jwt-old", Now.AddHours(1)),
            TokenAnswer("jwt-new", Now.AddHours(3)));
        using var certificate = TestCertificate.Create();
        var (_, authenticator) = CreateAuthenticator(dgii, clock);

        var first = await authenticator.AuthenticateAsync(IssuerRnc, certificate);

        clock.Advance(TimeSpan.FromHours(2));
        var second = await authenticator.AuthenticateAsync(IssuerRnc, certificate);

        dgii.SeedRequests.Should().Be(2);
        dgii.TokenRequests.Should().Be(2);
        second.Token.Should().Be("jwt-new");
        second.Token.Should().NotBe(first.Token);
    }

    [Fact]
    public async Task AuthenticateAsync_WhenTheDgiiRefusesToRenew_ThrowsWithTheDgiiError()
    {
        var clock = new FakeTimeProvider(Now);
        var dgii = new DgiiStub(
            TokenAnswer("jwt-old", Now.AddHours(1)),
            Refusal("El RNC no esta autorizado"));
        using var certificate = TestCertificate.Create();
        var (_, authenticator) = CreateAuthenticator(dgii, clock);

        await authenticator.AuthenticateAsync(IssuerRnc, certificate);
        clock.Advance(TimeSpan.FromHours(2));

        var act = () => authenticator.AuthenticateAsync(IssuerRnc, certificate);

        // The call fails with what the DGII said, never with the token that
        // already died: a stale token would be rejected with no explanation.
        var exception = await act.Should().ThrowAsync<DgiiException>();
        exception.Which.RawBody.Should().Contain("El RNC no esta autorizado");
        dgii.SeedRequests.Should().Be(2);

        // And it keeps failing the same way: the dead token is never served.
        var again = () => authenticator.AuthenticateAsync(IssuerRnc, certificate);
        await again.Should().ThrowAsync<DgiiException>();
    }

    [Fact]
    public async Task AuthenticateAsync_WhenTwoEmissionsOfTheSameIssuerRunAtTheSameTime_MakesOneTripToTheDgii()
    {
        var seedArrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSeed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var dgii = new DgiiStub(TokenAnswer("jwt", Now.AddHours(1)));
        dgii.BeforeSeedAnswer = async () =>
        {
            seedArrived.TrySetResult();
            await releaseSeed.Task;
        };

        using var certificate = TestCertificate.Create();
        var (_, authenticator) = CreateAuthenticator(dgii, new FakeTimeProvider(Now));

        var first = authenticator.AuthenticateAsync(IssuerRnc, certificate);
        await seedArrived.Task;
        var second = authenticator.AuthenticateAsync(IssuerRnc, certificate);
        releaseSeed.TrySetResult();

        var tokens = await Task.WhenAll(first, second);

        // The two emissions share the trip: one seed and one token.
        dgii.SeedRequests.Should().Be(1);
        dgii.TokenRequests.Should().Be(1);
        tokens[0].Token.Should().Be(tokens[1].Token);
    }

    [Fact]
    public async Task AuthenticateAsync_WithTwoAuthenticatorsOverTheSameCache_MakesOneTripToTheDgii()
    {
        var dgii = new DgiiStub(TokenAnswer("jwt", Now.AddHours(1)));
        using var certificate = TestCertificate.Create();
        var handler = dgii.CreateHandler();
        var cache = new DgiiTokenCache(new FakeTimeProvider(Now));

        // The authenticator is transient, so the host hands every resolution
        // its own: the cache is what they share.
        var first = await AuthenticatorOver(handler, cache).AuthenticateAsync(IssuerRnc, certificate);
        var second = await AuthenticatorOver(handler, cache).AuthenticateAsync(IssuerRnc, certificate);

        dgii.SeedRequests.Should().Be(1);
        dgii.TokenRequests.Should().Be(1);
        second.Token.Should().Be(first.Token);
    }

    private static (DgiiClient Client, DgiiAuthenticator Authenticator) CreateAuthenticator(
        DgiiStub dgii,
        TimeProvider clock)
    {
        var handler = dgii.CreateHandler();
        var client = new DgiiClient(new HttpClient(handler), new DgiiOptions());

        return (client, new DgiiAuthenticator(client, new XmlDsigEcfSigner(), new DgiiTokenCache(clock)));
    }

    private static DgiiAuthenticator AuthenticatorOver(StubHttpMessageHandler handler, DgiiTokenCache cache)
        => new(new DgiiClient(new HttpClient(handler), new DgiiOptions()), new XmlDsigEcfSigner(), cache);

    private static Func<HttpResponseMessage> TokenAnswer(string token, DateTimeOffset expiresAt)
        => () => Json($$"""{"token":"{{token}}","expira":"{{Format(expiresAt)}}","expedido":"{{Format(Now)}}"}""");

    private static Func<HttpResponseMessage> Refusal(string message)
        => () => Json($$"""{"error":"{{message}}"}""", HttpStatusCode.InternalServerError);

    private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK)
        => new(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private static string Format(DateTimeOffset value)
        => value.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture) + "Z";

    // The DGII as the tests see it: it answers the seed, the tokens and the
    // reception, and counts every call, because a trip is what the cache must
    // avoid.
    private sealed class DgiiStub
    {
        private readonly Queue<Func<HttpResponseMessage>> _tokenAnswers;
        private Func<HttpResponseMessage>? _lastAnswer;

        public DgiiStub(params Func<HttpResponseMessage>[] tokenAnswers)
        {
            _tokenAnswers = new Queue<Func<HttpResponseMessage>>(tokenAnswers);
        }

        // Runs before the seed is answered, to hold a trip open.
        public Func<Task> BeforeSeedAnswer { get; set; } = () => Task.CompletedTask;

        public StubHttpMessageHandler Handler { get; private set; } = null!;

        public int SeedRequests { get; private set; }

        public int TokenRequests { get; private set; }

        public string? LastTokenBody { get; private set; }

        public StubHttpMessageHandler CreateHandler()
            => Handler = new(async (request, _) => await AnswerAsync(request));

        private async Task<HttpResponseMessage> AnswerAsync(HttpRequestMessage request)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;

            if (path.EndsWith(DgiiEndpoints.Seed, StringComparison.Ordinal))
            {
                SeedRequests++;
                await BeforeSeedAnswer();

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(SeedXml, Encoding.UTF8, "text/xml")
                };
            }

            if (path.EndsWith(DgiiEndpoints.ValidateSeed, StringComparison.Ordinal))
            {
                TokenRequests++;
                LastTokenBody = request.Content is null
                    ? null
                    : await request.Content.ReadAsStringAsync();

                // A trip the test did not expect is answered like the last one:
                // the counters are what say that the trip happened.
                if (_tokenAnswers.Count > 0)
                    _lastAnswer = _tokenAnswers.Dequeue();

                return (_lastAnswer ?? (() => new HttpResponseMessage(HttpStatusCode.InternalServerError)))();
            }

            return Json("""{"trackId":"track-1"}""");
        }
    }

    // A clock the test moves by hand, so an expiration can be observed without
    // waiting the hour it takes.
    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan amount) => _now = _now.Add(amount);
    }
}
