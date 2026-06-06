using Volterp.Api.Configuration;
using Volterp.Api.Helpers;
using Volterp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

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

ServiceExtensions.ConfigureDbContext(builder.Services, dbSettings);
ServiceExtensions.ConfigureUnitOfWork(builder.Services);
ServiceExtensions.ConfigureServiceManager(builder.Services);
ServiceExtensions.ConfigureJwt(builder.Services, jwtSettings);
ServiceExtensions.ConfigureControllers(builder.Services);
ServiceExtensions.ConfigureCors(builder.Services);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<VolterpDbContext>();
    db.Database.Migrate();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();