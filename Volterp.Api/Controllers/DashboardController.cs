using Volterp.Domain.Enums;

namespace Volterp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController(IServiceManager serviceManager) : BaseController
{
    [HttpGet]
    public async Task<ActionResult<DashboardResponse>> GetDashboard(
        [FromQuery] string? period = null,
        [FromQuery] string? chartPeriod = null,
        CancellationToken ct = default)
    {
        EnsureTenant();

        if (!TryParseScope(period, out var scope))
            return BadRequest(new ErrorResponse(
                $"Invalid period '{period}'. Accepted values are: today, week, month."));

        if (!TryParseChartWindow(chartPeriod, out var chartWindow))
            return BadRequest(new ErrorResponse(
                $"Invalid chartPeriod '{chartPeriod}'. Accepted values are: week, month, year."));

        var result = await serviceManager.Dashboard.GetDashboardDataAsync(scope, chartWindow, ct);

        return result.Match<ActionResult<DashboardResponse>>(
            error => BadRequest(new ErrorResponse(error.Message)),
            data => Ok(data)
        );
    }

    // An absent or blank period means the calendar month. An unknown value is an error:
    // falling back to a default would answer a question nobody asked.
    private static bool TryParseScope(string? value, out DashboardScope scope)
    {
        scope = DashboardScope.Month;

        if (string.IsNullOrWhiteSpace(value))
            return true;

        switch (value.Trim().ToLowerInvariant())
        {
            case "today":
                scope = DashboardScope.Today;
                return true;
            case "week":
                scope = DashboardScope.Week;
                return true;
            case "month":
                scope = DashboardScope.Month;
                return true;
            default:
                return false;
        }
    }

    // An absent or blank chart period means the current week. An unknown value is an
    // error: falling back to a default would answer a question nobody asked.
    private static bool TryParseChartWindow(string? value, out ChartWindow chartWindow)
    {
        chartWindow = ChartWindow.Week;

        if (string.IsNullOrWhiteSpace(value))
            return true;

        switch (value.Trim().ToLowerInvariant())
        {
            case "week":
                chartWindow = ChartWindow.Week;
                return true;
            case "month":
                chartWindow = ChartWindow.Month;
                return true;
            case "year":
                chartWindow = ChartWindow.Year;
                return true;
            default:
                return false;
        }
    }
}
