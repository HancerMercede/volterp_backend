using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Volterp.Application.Abstractions;
using Volterp.Application.Common;

namespace Volterp.Api.Http;

public sealed class HttpTenantContext(IHttpContextAccessor httpContextAccessor) : ITenantContext
{
    public Guid? CompanyId
    {
        get
        {
            var claim = httpContextAccessor.HttpContext?.User.FindFirst(Claims.CompanyId)?.Value;
            return Guid.TryParse(claim, out var id) ? id : null;
        }
    }
}