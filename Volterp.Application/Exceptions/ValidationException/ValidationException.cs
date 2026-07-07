using Volterp.Application.Interfaces;

namespace Volterp.Application.Exceptions;

/// <summary>
/// 
/// </summary>
public class ValidationExecption : Exception, IHttpException
{
    public int HttpStatusCode => 422;
    public int ErrorCode { get; }
    public object _values { get; }

    public override string Message => base.Message;

    public ValidationExecption(string message, object info)
        : base(message)
    {
        _values = info;
    }

    public ValidationExecption(string message, string info, Exception innerException)
        : base(message, innerException)
    {
        _values = info;
    }

    public ValidationExecption(string message, string info, int errorCode)
        : base(message)
    {
        ErrorCode = errorCode;
        _values = info;
    }

    public ValidationExecption(string message, string info, int errorCode, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        _values = info;
    }

    public override string ToString()
    {
        return $"ValidationExecption: ErrorCode={ErrorCode}, Message={Message}";
    }
}