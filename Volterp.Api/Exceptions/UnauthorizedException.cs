using Volterp.Application.Interfaces;

namespace Volterp.Api.Exceptions;

public class UnauthorizedException : Exception, IHttpException
{
    public int HttpStatusCode => 401;

    public UnauthorizedException(string? message) : base(message) { }
}