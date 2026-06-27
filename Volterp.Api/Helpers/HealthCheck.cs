using Volterp.Infrastructure.Data;

namespace Volterp.Api.Helpers;

public class HealthCheck
{
    public static async Task<List<Object>> Init(VolterpDbContext db)
    {
        var checks = new List<object>
        {
            // ── API check ──
            new { service = "API", status = "healthy", latencyMs = 0 }
        };

        // ── Database check ──
        var dbStopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var canConnect = await db.Database.CanConnectAsync();
            dbStopwatch.Stop();
            checks.Add(new
            {
                service = "Database",
                status = canConnect ? "healthy" : "unhealthy",
                latencyMs = dbStopwatch.ElapsedMilliseconds
            });
        }
        catch
        {
            dbStopwatch.Stop();
            checks.Add(new
            {
                service = "Database",
                status = "unhealthy",
                latencyMs = dbStopwatch.ElapsedMilliseconds,
                error = "Cannot connect to database"
            });
        }
        return checks;
    }
}