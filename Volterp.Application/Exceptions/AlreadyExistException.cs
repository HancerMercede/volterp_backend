using Volterp.Application.Interfaces;

namespace Volterp.Application.Exceptions;

/// <summary>
/// Base exception for duplicate/conflict scenarios → 409 Conflict.
/// Use the typed entity subclasses (e.g. <see cref="Category.CategoryAlreadyExistException"/>)
/// for specific domain errors, or throw this directly for generic cases.
/// </summary>
public class AlreadyExistException : Exception, IHttpException
{
    public int HttpStatusCode => 409;
    public int ErrorCode { get; }
    public object? Values { get; }

    public AlreadyExistException() { }

    public AlreadyExistException(string? message) : base(message) { }

    public AlreadyExistException(string? message, object? values) : base(message)
    {
        Values = values;
    }

    public AlreadyExistException(string? message, object? values, int errorCode) : base(message)
    {
        Values = values;
        ErrorCode = errorCode;
    }

    public AlreadyExistException(string? message, Exception? innerException) : base(message, innerException) { }

    public AlreadyExistException(string? message, object? values, Exception? innerException) : base(message, innerException)
    {
        Values = values;
    }
}
