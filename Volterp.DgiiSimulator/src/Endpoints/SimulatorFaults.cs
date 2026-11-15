using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Volterp.DgiiSimulator;

/// <summary>
/// The failures the simulator injects on purpose, so the resilience of a
/// client can be exercised without a real outage. They are answers the DGII
/// could give, never a new endpoint.
/// </summary>
internal static class SimulatorFaults
{
    public static async Task Apply(HttpContext context, RequestDelegate next)
    {
        var options = context.RequestServices.GetRequiredService<DgiiSimulatorOptions>();

        var forced = context.Request.Headers[SimulatorControl.ErrorHeader].ToString();
        if (!string.IsNullOrWhiteSpace(forced)
            && int.TryParse(forced, out var status)
            && status is >= 400 and <= 599)
        {
            await Fail(context, status);
            return;
        }

        if (options.FailureRate > 0 && Random.Shared.NextDouble() < options.FailureRate)
        {
            await Fail(context, StatusCodes.Status500InternalServerError);
            return;
        }

        await next(context);
    }

    private static async Task Fail(HttpContext context, int status)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";

        var detail = JsonSerializer.Serialize(new
        {
            status,
            title = "Fallo inyectado por el simulador.",
            detail = "El simulador respondió con un error a propósito."
        });

        await context.Response.WriteAsync(detail, context.RequestAborted);
    }
}
