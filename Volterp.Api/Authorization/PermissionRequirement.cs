using Microsoft.AspNetCore.Authorization;
using Volterp.Domain.Authorization;

namespace Volterp.Api.Authorization;

// What an endpoint needs to let a caller in: one permission of the catalog.
//
// The permission is declared by the endpoint, not guessed by the framework:
// an endpoint that does not declare it is an endpoint that is open to any
// authenticated caller, and that is what the inventory test of the endpoints
// fails on.
public class PermissionRequirement : IAuthorizationRequirement
{
    // Where the permission that the endpoint asked for is left for the
    // refusal: the message of the 403 names the permission that is missing.
    public const string RequiredPermissionItem = "volterp.required-permission";

    public PermissionRequirement(Permission permission) => Permission = permission;

    public Permission Permission { get; }
}
