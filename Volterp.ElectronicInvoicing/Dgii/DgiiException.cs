using System.Net;

namespace Volterp.ElectronicInvoicing.Dgii;

public sealed class DgiiException : Exception
{
    public DgiiException(
        string message,
        string? rawBody = null,
        Exception? innerException = null,
        HttpStatusCode? statusCode = null)
        : base(message, innerException)
    {
        RawBody = rawBody;
        StatusCode = statusCode;
    }

    public string? RawBody { get; }
    public HttpStatusCode? StatusCode { get; }
}
