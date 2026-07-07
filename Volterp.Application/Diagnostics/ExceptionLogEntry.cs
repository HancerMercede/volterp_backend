namespace Volterp.Application.Diagnostics;

public class ExceptionLogEntry
{
    public DateTime Timestamp { get; init; }
    public string ExceptionType { get; init; } = "";
    public string Message { get; init; } = "";
    public int StatusCode { get; init; }
    public string TraceId { get; init; } = "";
    public string Path { get; init; } = "";
    public string? Detail { get; init; }
}