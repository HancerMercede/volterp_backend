using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Volterp.Application.Diagnostics;
using Volterp.Application.Exceptions;
using Volterp.Application.Interfaces;

namespace Volterp.Api.Extensions;

public static class ExceptionMiddleWareHandler
{
    public static void ConfigureExceptionMiddleWare(this WebApplication app, ILogger logger)
    {
        app.UseExceptionHandler(appError =>
        {
            appError.Run(async context =>
            {
                var contextFeature = context.Features.Get<IExceptionHandlerFeature>();
                if (contextFeature is null) return;

                var exception = contextFeature.Error;
                var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
                var path = context.Request.Path;

                var statusCode = exception is IHttpException httpEx
                    ? httpEx.HttpStatusCode
                    : StatusCodes.Status500InternalServerError;

                context.Response.StatusCode = statusCode;
                context.Response.ContentType = "application/problem+json";

                // -- Log Level depend on status code --
                var (level, message) = statusCode switch
                {
                    >= 500 => (LogLevel.Error, "Unhandled exception {Method} {Path}"),
                    >= 400 => (LogLevel.Warning, "Handled application exception {Method} {Path}"),
                    _ => (LogLevel.Information, "Recovered exception {Method} {Path}")
                };
                logger.Log(level, exception, message, context.Request.Method, path);

                // -- Build RFC 7807 ProblemDetails --
                var problem = new ProblemDetails
                {
                    Status = statusCode,
                    Title = GetDefaultTitle(statusCode, exception),
                    Detail = app.Environment.IsDevelopment()
                        ? exception.ToString()
                        : null,
                    Instance = path,
                    Type = $"https://httpstatuses.io/{statusCode}"
                };
                problem.Extensions["traceId"] = traceId;
                problem.Extensions["timestamp"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

                // ── Expose ValidationException._values as data ──
                if (exception is ValidationExecption valEx && valEx._values is not null)
                {
                    problem.Extensions["data"] = SerializeDetails(valEx._values);
                }

                // ── Bitácora en memoria ──
                var logStore = context.RequestServices.GetService<IExceptionLogStore>();
                logStore?.Record(exception, statusCode, traceId, path, problem.Detail);

                var json = JsonSerializer.Serialize(problem);
                await context.Response.WriteAsync(json);
            });


        });
    }
    private static string GetDefaultTitle(int statusCode, Exception exception)
    {
        if (statusCode >= 500)
            return "Internal Server Error.";
        if (statusCode >= 400)
            return exception.Message;
        return "Unexpected error.";
    }

    private static object? SerializeDetails(object values)
    {
        if (values is string s) return s;
        try
        {
            // Try to round-trip through JsonElement so it serializes cleanly
            var el = JsonSerializer.SerializeToElement(values);
            return el;
        }
        catch
        {
            return values.ToString();
        }
    }
}