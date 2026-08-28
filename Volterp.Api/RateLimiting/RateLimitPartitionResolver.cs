using System.Net;
using System.Security.Claims;

namespace Volterp.Api.RateLimiting;

public sealed class RateLimitPartitionResolver
{
    private const string UserPrefix = "user:";
    private const string IpPrefix = "ip:";

    public string GetGlobalPartitionKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var userKey = TryGetAuthenticatedKey(context.User);
        return userKey is not null
            ? UserPrefix + userKey
            : IpPrefix + NormalizeRemoteIp(context);
    }

    public string GetAuthenticationPartitionKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return IpPrefix + NormalizeRemoteIp(context);
    }

    private static string? TryGetAuthenticatedKey(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
            return null;

        var userId = user.FindFirstValue("userId");
        if (!string.IsNullOrWhiteSpace(userId))
            return userId.Trim();

        var nameId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(nameId))
            return nameId.Trim();

        var name = user.FindFirstValue(ClaimTypes.Name);
        if (!string.IsNullOrWhiteSpace(name))
            return name.Trim();

        return null;
    }

    private static string NormalizeRemoteIp(HttpContext context)
    {
        var ip = context.Connection.RemoteIpAddress;
        if (ip is null)
            return "unknown";
        if (ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();
        return ip.ToString();
    }
}