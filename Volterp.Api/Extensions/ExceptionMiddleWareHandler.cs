using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Volterp.Application.Diagnostics;
using Volterp.Application.DTOs;
using Volterp.Application.Exceptions.ValidationException;

namespace Volterp.Api.Extensions;

public static class ExceptionMiddleWareHandler
{
    // The house writes JSON in camelCase (the controllers use the same shape),
    // and the middleware writes the body by hand instead of through MVC.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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

                // -- Log Level depend on status code --
                var (level, message) = statusCode switch
                {
                    >= 500 => (LogLevel.Error, "Unhandled exception {Method} {Path}"),
                    >= 400 => (LogLevel.Warning, "Handled application exception {Method} {Path}"),
                    _ => (LogLevel.Information, "Recovered exception {Method} {Path}")
                };
                logger.Log(level, exception, message, context.Request.Method, path);

                // The stack trace only travels in development.
                var detail = app.Environment.IsDevelopment()
                    ? exception.ToString()
                    : null;
                var details = DetailsOf(exception);

                // -- Bitácora en memoria --
                var logStore = context.RequestServices.GetService<IExceptionLogStore>();
                logStore?.Record(exception, statusCode, traceId, path, detail);

                // -- 5xx travels in RFC 7807 ProblemDetails --
                if (statusCode >= 500)
                {
                    context.Response.ContentType = "application/problem+json";

                    var problem = new ProblemDetails
                    {
                        Status = statusCode,
                        Title = GetDefaultTitle(statusCode, exception),
                        Detail = detail,
                        Instance = path,
                        Type = $"https://httpstatuses.io/{statusCode}"
                    };
                    problem.Extensions["traceId"] = traceId;
                    problem.Extensions["timestamp"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

                    await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
                    return;
                }

                // -- 4xx travels in the same envelope every other error of the API uses --
                context.Response.ContentType = "application/json";

                var error = new ErrorResponse(GetDefaultTitle(statusCode, exception), details);
                await context.Response.WriteAsync(JsonSerializer.Serialize(error, JsonOptions));
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

    // The validation data travels in the `details` of the envelope, serialized
    // so the client reads the same shape whatever the exception carried.
    private static string? DetailsOf(Exception exception)
    {
        if (exception is not ValidationExecption valEx || valEx._values is null)
            return null;

        return valEx._values is string text
            ? text
            : JsonSerializer.Serialize(valEx._values);
    }
}