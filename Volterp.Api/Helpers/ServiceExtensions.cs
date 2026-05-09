using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Volterp.Api.Configuration;
using Volterp.Application.Interfaces;
using Volterp.Application.Services;
using Volterp.Infrastructure.Data;
using Volterp.Infrastructure.Services;
using Volterp.Infrastructure.UnitOfWork;

namespace Volterp.Api.Helpers;

public static class ServiceExtensions
{
    public static void ConfigureDbContext(IServiceCollection services, DatabaseSettings settings)
        => services.AddDbContext<VolterpDbContext>(options =>
            options.UseNpgsql(settings.DefaultConnection));

    public static void ConfigureUnitOfWork(IServiceCollection services)
        => services.AddScoped<IUnitOfWork, UnitOfWork>();

    public static void ConfigureServiceManager(IServiceCollection services)
        => services.AddScoped<IServiceManager, ServiceManager>();
    
    public static void ConfigureJwt(IServiceCollection services, JwtSettings jwtSettings)
    {
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidAudience = jwtSettings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key))
                };
            });

        services.AddAuthorization();
    }

    public static void ConfigureControllers(IServiceCollection services)
        => services.AddControllers();

    public static void ConfigureCors(IServiceCollection services)
        => services.AddCors(options =>
            options.AddDefaultPolicy(policy =>
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader()));
}