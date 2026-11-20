using Volterp.Api.Authorization;
using Volterp.Application.DTOs;
using Volterp.Application.Interfaces;

namespace Volterp.Api.Controllers;

// What the caller may do, and what every role of the catalog grants.
//
// The front reads the permissions of the caller from here and stops keeping
// a matrix of its own (S6): the union of the permissions of the roles is
// resolved in the server.
[ApiController]
[Route("api/permissions")]
[Authorize]
public class PermissionsController(IPermissionService permissionService) : BaseController
{
    [HttpGet("me")]
    public IActionResult GetMe()
    {
        var roles = CallerRoles.Of(User);

        return Ok(new MePermissionsDto(
            roles.Distinct().ToArray(),
            permissionService.EffectivePermissions(roles).ToArray()));
    }

    [HttpGet("roles")]
    public IActionResult GetRoles()
        => Ok(new RolesPermissionsDto(permissionService.Matrix().ToArray()));
}
