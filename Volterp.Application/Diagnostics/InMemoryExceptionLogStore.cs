using System.Collections.Concurrent;

namespace Volterp.Application.Diagnostics;

public class InMemoryExceptionLogStore : IExceptionLogStore
{
    private readonly ConcurrentQueue<ExceptionLogEntry> _queue = new();
    private readonly int _capacity;

    public InMemoryExceptionLogStore(int capacity = 100)
    {
        _capacity = capacity;
    }

    public void Record(Exception exception, int statusCode, string traceId, string path, string? detail)
    {
        var entry = new ExceptionLogEntry
        {
            Timestamp = DateTime.UtcNow,
            ExceptionType = exception.GetType().Name,
            Message = exception.Message,
            StatusCode = statusCode,
            TraceId = traceId,
            Path = path,
            Detail = detail
        };

        _queue.Enqueue(entry);

        while (_queue.Count > _capacity && _queue.TryDequeue(out _)) { }
    }

    public IReadOnlyList<ExceptionLogEntry> GetRecent(int count = 20)
    {
        return _queue.Reverse().Take(count).ToList().AsReadOnly();
    }
}