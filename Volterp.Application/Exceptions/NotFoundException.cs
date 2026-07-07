using Volterp.Application.Interfaces;

namespace Volterp.Application.Exceptions;

/// <summary>
/// Base exception for entity not found scenarios → 404 Not Found.
/// Use the typed entity subclasses (e.g. <see cref="Category.CategoryNotFoundException"/>)
/// for specific domain errors, or throw this directly for generic cases.
/// </summary>
public class NotFoundException : Exception, IHttpException
{
    public int HttpStatusCode => 404;
    public int ErrorCode { get; }
    public object? Values { get; }

    public NotFoundException() { }

    public NotFoundException(string? message) : base(message) { }

    public NotFoundException(string? message, object? values) : base(message)
    {
        Values = values;
    }

    public NotFoundException(string? message, object? values, int errorCode) : base(message)
    {
        Values = values;
        ErrorCode = errorCode;
    }

    public NotFoundException(string? message, Exception? innerException) : base(message, innerException) { }

    public NotFoundException(string? message, object? values, Exception? innerException) : base(message, innerException)
    {
        Values = values;
    }
}
