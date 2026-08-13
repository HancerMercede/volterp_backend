using Volterp.Api.Configuration;
using Volterp.Api.Helpers;
using Volterp.Application.Diagnostics;
using Volterp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Volterp.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

var jwtSettings = new JwtSettings
{
    Key = builder.Configuration["Jwt:Key"]!,
    Issuer = builder.Configuration["Jwt:Issuer"]!,
    Audience = builder.Configuration["Jwt:Audience"]!,
    ExpiryMinutes = int.Parse(builder.Configuration["Jwt:ExpiryMinutes"]!)
};

var dbSettings = new DatabaseSettings
{
    DefaultConnection = builder.Configuration.GetConnectionString("DefaultConnection")!
};

builder.Services.ConfigureDbContext(dbSettings);
ServiceExtensions.ConfigureUnitOfWork(builder.Services);
ServiceExtensions.ConfigureServiceManager(builder.Services);
ServiceExtensions.ConfigureJwt(builder.Services, jwtSettings);
ServiceExtensions.ConfigureControllers(builder.Services);
ServiceExtensions.ConfigureCors(builder.Services);
ServiceExtensions.ConfigureExceptionLogStore(builder.Services);
ServiceExtensions.ConfigureCurrentUser(builder.Services);
builder.Services.ConfigureForwardedHeaders();
builder.Services.ConfigureRateLimiting(builder.Configuration);
builder.Services.ConfigureSwagger();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<VolterpDbContext>();
    db.Database.Migrate();
}

app.ConfigureExceptionMiddleWare(app.Logger);
app.UseForwardedHeaders();
app.UseCors();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Volterp ERP API v1");
    });
}

app.MapControllers();
app.MapGet("health", async (VolterpDbContext db) =>
{
    var checks = await HealthCheck.Init(db);
    var allHealthy = checks.All(c => c.GetType().GetProperty("status")?.GetValue(c)?.ToString() == "healthy");
    return Results.Ok(new
    {
        status = allHealthy ? "healthy" : "degraded",
        timestamp = DateTime.UtcNow,
        checks
    });
}).DisableRateLimiting();
app.MapGet("/_diagnostics/exceptions", (IExceptionLogStore store) =>
    Results.Ok(store.GetRecent(50)));
app.UseStaticFiles();
app.Run();