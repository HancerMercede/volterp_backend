using Serilog;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.EntityFrameworkCore;
using Volterp.Api.Authentication;
using Volterp.Api.Authorization;
using Volterp.Api.Configuration;
using Volterp.Api.Controllers;
using Volterp.Api.Extensions;
using Volterp.Application.Diagnostics;
using Volterp.Application.Services;
using Volterp.Infrastructure.Data;



var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
    options.ConfigureHttpsDefaults(https =>
        https.ClientCertificateMode = ClientCertificateMode.AllowCertificate));

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

var encryptionSettings = new EncryptionSettings
{
    MasterKey = builder.Configuration["Encryption:MasterKey"]!
};

ServiceExtensions.ConfigureDbContext(builder.Services, dbSettings);
ServiceExtensions.ConfigureUnitOfWork(builder.Services);
ServiceExtensions.ConfigureServiceManager(builder.Services);
ServiceExtensions.ConfigureJwt(builder.Services, jwtSettings);
ServiceExtensions.ConfigureEncryption(builder.Services, encryptionSettings);
ServiceExtensions.ConfigureElectronicInvoicing(builder.Services, builder.Configuration);
builder.Services.AddAuthentication()
    .AddScheme<DgiiCertificateAuthenticationOptions, DgiiCertificateAuthenticationHandler>(
        ElectronicInvoiceReceptionController.DgiiAuthenticationScheme,
        "DGII client certificate",
        options =>
        {
            builder.Configuration
                .GetSection(DgiiCertificateAuthenticationOptions.SectionName)
                .Bind(options);
            options.LoadTrustedRootCertificates();
        });
ServiceExtensions.ConfigureControllers(builder.Services);
ServiceExtensions.ConfigureCors(builder.Services, builder.Configuration);
ServiceExtensions.ConfigureApiCredentials(builder.Services);
ServiceExtensions.ConfigureRangeAnnulment(builder.Services);
ServiceExtensions.ConfigureSeedAuthentication(builder.Services);
ServiceExtensions.ConfigureExceptionLogStore(builder.Services);
ServiceExtensions.ConfigureCurrentUser(builder.Services);
ServiceExtensions.ConfigurePermissions(builder.Services);
ServiceExtensions.ConfigureTenantContext(builder.Services);
builder.Services.ConfigureForwardedHeaders();
builder.Services.ConfigureRateLimiting(builder.Configuration);
builder.Services.ConfigureSwagger();

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();


builder.Host.UseSerilog();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<VolterpDbContext>();
    if (!app.Environment.IsEnvironment("Testing"))
        db.Database.Migrate();
}

app.ConfigureExceptionMiddleWare(app.Logger);
app.UseForwardedHeaders();
app.UseCors();
app.UseAuthentication();
app.UseRateLimiter();
app.UseTenantLogging();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Volterp ERP API v1");
        app.MapGet("/", () => Results.Redirect("/swagger"));  
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
    Results.Ok(store.GetRecent(50))).RequireAuthorization(PermissionPolicies.AdministrationRead);

app.UseStaticFiles();
app.Run();
