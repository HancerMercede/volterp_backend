namespace Volterp.Application.Diagnostics;

public interface IExceptionLogStore
{
    void Record(Exception exception, int statusCode, string traceId, string path, string? detail);
    IReadOnlyList<ExceptionLogEntry> GetRecent(int count = 20);
}