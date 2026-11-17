using System.Net;
using System.Security.Claims;
using Volterp.Application.Common;

namespace Volterp.Api.RateLimiting;

public sealed class RateLimitPartitionResolver
{
    private const string UserPrefix = "user:";
    private const string CompanyPrefix = "company:";
    private const string IpPrefix = "ip:";

    public string GetGlobalPartitionKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var userKey = TryGetAuthenticatedKey(context.User);
        if (userKey is not null)
            return userKey;

        // A machine has no user: its bucket belongs to its tenant and not to
        // the address it shares with the other machines behind it.
        var companyId = context.User.FindFirstValue(Claims.CompanyId);
        if (!string.IsNullOrWhiteSpace(companyId))
            return CompanyPrefix + companyId.Trim();

        return IpPrefix + NormalizeRemoteIp(context);
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

        var userId = user.FindFirstValue(Claims.UserId);
        if (!string.IsNullOrWhiteSpace(userId))
            return UserPrefix + userId.Trim();

        var nameId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(nameId))
            return UserPrefix + nameId.Trim();

        var name = user.FindFirstValue(ClaimTypes.Name);
        if (!string.IsNullOrWhiteSpace(name))
            return UserPrefix + name.Trim();

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