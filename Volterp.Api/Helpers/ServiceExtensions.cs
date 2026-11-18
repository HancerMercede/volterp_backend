using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Volterp.Api.Authentication;
using Volterp.Api.Configuration;
using Volterp.Api.Http;
using Volterp.Api.Workers;
using Volterp.Application.Abstractions;
using Volterp.Application.Diagnostics;
using Volterp.Application.DTOs;
using Volterp.Application.Interfaces;
using Volterp.Application.Services;
using Volterp.ElectronicInvoicing.Abstractions;
using Volterp.ElectronicInvoicing.Dgii;
using Volterp.ElectronicInvoicing.Signing;
using Volterp.ElectronicInvoicing.Xml;
using Volterp.Infrastructure.Data;
using Volterp.Infrastructure.Repositories;
using Volterp.Infrastructure.Services;
using Volterp.Infrastructure.UnitOfWork;

namespace Volterp.Api.Helpers;

public static class ServiceExtensions
{
    // The scheme of the JWT. It lives behind the default scheme, which is
    // the one that reads the header and decides who is calling.
    private const string JwtBearerSchemeName = "JwtBearer";

    public static void ConfigureDbContext(IServiceCollection services, DatabaseSettings settings)
        => services.AddDbContext<VolterpDbContext>(options =>
            options.UseNpgsql(settings.DefaultConnection));

    public static void ConfigureUnitOfWork(IServiceCollection services)
        => services.AddScoped<IUnitOfWork, UnitOfWork>();

    public static void ConfigureServiceManager(IServiceCollection services)
        => services.AddScoped<IServiceManager, ServiceManager>();
    
    public static void ConfigureJwt(IServiceCollection services, JwtSettings jwtSettings)
    {
        // The house writes JSON in camelCase, and these two answers are written
        // by hand instead of through MVC.
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            // The default scheme reads the header and forwards it: the two
            // kinds of caller travel in the same header and neither of them
            // knows about the other.
            .AddPolicyScheme(JwtBearerDefaults.AuthenticationScheme, "Bearer token or API key of a client", options =>
            {
                options.ForwardDefaultSelector = context => IsApiKeyRequest(context.Request)
                    ? ApiKeyAuthenticationHandler.SchemeName
                    : JwtBearerSchemeName;
            })
            .AddJwtBearer(JwtBearerSchemeName, options =>
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

                // The 401 and the 403 that the framework raises travel in the same
                // envelope every other error of the API uses.
                options.Events = new JwtBearerEvents
                {
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();

                        if (!context.Response.Headers.ContainsKey("WWW-Authenticate"))
                            context.Response.Headers.Append("WWW-Authenticate", "Bearer");

                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsync(
                            JsonSerializer.Serialize(new ErrorResponse("Unauthorized."), jsonOptions));
                    },
                    OnForbidden = async context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsync(
                            JsonSerializer.Serialize(new ErrorResponse("Forbidden."), jsonOptions));
                    }
                };
            })
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationHandler.SchemeName,
                "API key of a client",
                _ => { });

        services.AddAuthorization();
    }

    public static void ConfigureApiCredentials(IServiceCollection services)
    {
        services.AddScoped<IApiCredentialRepository, ApiCredentialRepository>();
        services.AddScoped<IApiCredentialService, ApiCredentialService>();
    }

    public static void ConfigureRangeAnnulment(IServiceCollection services)
    {
        services.AddScoped<IRangeAnnulmentService, RangeAnnulmentService>();
    }

    public static void ConfigureSeedAuthentication(IServiceCollection services)
    {
        services.AddScoped<IIssuedSeedRepository, IssuedSeedRepository>();
        services.AddScoped<ISeedAuthenticationService, SeedAuthenticationService>();

        // The token of the third party is the session it opened against this
        // platform, and it outlives the request that opened it.
        services.AddSingleton<ThirdPartyTokenCache>();
        services.AddSingleton<ISeedTokenCache>(provider => provider.GetRequiredService<ThirdPartyTokenCache>());
    }

    private static bool IsApiKeyRequest(HttpRequest request)
        => request.Headers.Authorization.ToString()
            .StartsWith(ApiKeyAuthenticationHandler.HeaderPrefix, StringComparison.OrdinalIgnoreCase);

    public static void ConfigureEncryption(IServiceCollection services, EncryptionSettings settings)
        => services.AddSingleton<ICredentialProtector>(new AesCredentialProtector(settings.MasterKey));

    public static void ConfigureElectronicInvoicing(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IEcfSigner, XmlDsigEcfSigner>();
        services.AddSingleton(new EcfXmlWriter());

        var dgiiOptions = new DgiiOptions();
        configuration.GetSection(DgiiOptions.SectionName).Bind(dgiiOptions);
        services.AddSingleton(dgiiOptions);

        services.AddHttpClient<IDgiiClient, DgiiClient>();
        services.AddSingleton(TimeProvider.System);

        // The token the DGII issues belongs to the issuer and lives an hour,
        // so the cache that keeps it is a singleton. The authenticator stays
        // transient: it holds the typed client for one resolution only, and a
        // singleton would capture that client for the life of the process and
        // pin the address of the DGII.
        services.AddSingleton<DgiiTokenCache>();
        services.AddTransient<DgiiAuthenticator>();

        // The host port of the library: the ERP resolves the issuer's
        // certificate from the encrypted credential of the company.
        services.AddScoped<ISigningCertificateProvider, CompanySigningCertificateProvider>();

        // The emission runs without an HTTP context: the company travels by
        // hand on every call.
        services.AddScoped<IElectronicInvoiceService, ElectronicInvoiceService>();
        services.AddHostedService<ElectronicInvoiceQueueWorker>();
    }

    public static void ConfigureControllers(IServiceCollection services)
        => services.AddControllers()
            .AddJsonOptions(options =>
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

    public static void ConfigureCors(IServiceCollection services, IConfiguration configuration)
    {
        // The origins that may call the API are the ones of the configuration:
        // the front of the ERP and the clients that rent the API.
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        services.AddCors(options =>
            options.AddDefaultPolicy(policy =>
                policy.WithOrigins(origins)
                      .AllowAnyHeader()
                      .AllowAnyMethod()));
    }

    public static void ConfigureExceptionLogStore(IServiceCollection services)
        => services.AddSingleton<IExceptionLogStore>(new InMemoryExceptionLogStore(100));

    public static void ConfigureCurrentUser(IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
    }

    public static void ConfigureTenantContext(IServiceCollection services)
        => services.AddScoped<ITenantContext, HttpTenantContext>();

    public static void ConfigureRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var rateLimiting = new RateLimitingOptions();
        configuration.GetSection(RateLimitingOptions.SectionName).Bind(rateLimiting);
        Validator.ValidateObject(rateLimiting, new ValidationContext(rateLimiting), validateAllProperties: true);

        services.AddSingleton(rateLimiting);
        services.AddSingleton<RateLimitPartitionResolver>();

        services.AddRateLimiter(opt =>
        {
            opt.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            opt.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                var resolver = httpContext.RequestServices.GetRequiredService<RateLimitPartitionResolver>();
                var policy = httpContext.User.Identity?.IsAuthenticated == true
                    ? rateLimiting.Authenticated
                    : rateLimiting.Anonymous;
                var key = resolver.GetGlobalPartitionKey(httpContext);

                return RateLimitPartition.GetSlidingWindowLimiter(key, _ => new SlidingWindowRateLimiterOptions
                {
                    AutoReplenishment = true,
                    PermitLimit = policy.PermitLimit,
                    Window = TimeSpan.FromSeconds(policy.WindowSeconds),
                    SegmentsPerWindow = 6,
                    QueueLimit = 0,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                });
            });

            opt.AddPolicy(RateLimiterPolicies.Authentication, httpContext =>
            {
                var resolver = httpContext.RequestServices.GetRequiredService<RateLimitPartitionResolver>();
                var key = resolver.GetAuthenticationPartitionKey(httpContext);
                return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
                {
                    AutoReplenishment = true,
                    PermitLimit = rateLimiting.Authentication.PermitLimit,
                    Window = TimeSpan.FromSeconds(rateLimiting.Authentication.WindowSeconds),
                    QueueLimit = 0
                });
            });

            opt.OnRejected = OnRejectedAsync;
        });
    }

    public static void ConfigureForwardedHeaders(this IServiceCollection services)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;

            if (!options.KnownProxies.Contains(IPAddress.Loopback))
                options.KnownProxies.Add(IPAddress.Loopback);
            if (!options.KnownProxies.Contains(IPAddress.IPv6Loopback))
                options.KnownProxies.Add(IPAddress.IPv6Loopback);
        });
    }

    public static void ConfigureSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();

        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Volterp ERP API",
                Version = "v1",
                Description = Constants.Description
            });

            // JWT Bearer security scheme so the Swagger UI exposes an "Authorize" button.
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT"
            });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });

            // XML documentation — only loaded if the file exists (XML docs are added progressively).
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
                c.IncludeXmlComments(xmlPath);
        });
    }

    private static async ValueTask OnRejectedAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var response = context.HttpContext.Response;
        response.StatusCode = StatusCodes.Status429TooManyRequests;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds))
                .ToString(CultureInfo.InvariantCulture);
        }

        await response.WriteAsJsonAsync(
            new ErrorResponse("The request was rate-limited. Retry later."),
            cancellationToken);
    }
}