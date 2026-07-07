namespace Volterp.Application.Interfaces;

public interface IHttpException
{
    int  HttpStatusCode { get; }
}