using Serilog.Context;
using Volterp.Application.Abstractions;

namespace Volterp.Api.Extensions;

public sealed class TenantLoggingMiddleware(RequestDelegate next, ITenantContext tenantContext)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var companyId = tenantContext.CompanyId?.ToString();
        using (LogContext.PushProperty("CompanyId", companyId))
        {
            await next(context);
        }
    }
}

public static class TenantLoggingExtensions
{
    extension(IApplicationBuilder builder)
    {
        public void UseTenantLogging()
            => builder.UseMiddleware<TenantLoggingMiddleware>();
    }
}
