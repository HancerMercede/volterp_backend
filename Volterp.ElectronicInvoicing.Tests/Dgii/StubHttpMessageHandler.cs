using System.Net;

namespace Volterp.ElectronicInvoicing.Tests.Dgii;

/// <summary>
/// A handler that answers with whatever the test decides and keeps what the
/// client sent, so the assertions are about the request the DGII would receive.
/// </summary>
internal sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
    : HttpMessageHandler
{
    public string? LastUri { get; private set; }
    public string? LastMethod { get; private set; }
    public string? LastBody { get; private set; }
    public string? LastContentType { get; private set; }
    public string? LastAuthorization { get; private set; }
    public string? LastAccept { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        LastUri = request.RequestUri?.ToString();
        LastMethod = request.Method.Method;
        LastContentType = request.Content?.Headers.ContentType?.ToString();
        LastAuthorization = request.Headers.TryGetValues("Authorization", out var authorization)
            ? authorization.FirstOrDefault()
            : null;
        LastAccept = request.Headers.Accept.FirstOrDefault()?.MediaType;
        LastBody = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);

        return responder(request);
    }

    public static StubHttpMessageHandler ReturningJson(string json, HttpStatusCode status = HttpStatusCode.OK)
        => new(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        });

    public static StubHttpMessageHandler ReturningText(string body, HttpStatusCode status = HttpStatusCode.OK)
        => new(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "text/xml")
        });
}
