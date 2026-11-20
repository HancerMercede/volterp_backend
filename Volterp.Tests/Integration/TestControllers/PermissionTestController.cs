using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volterp.Api.Authorization;

namespace Volterp.Tests.Integration.TestControllers;

// The endpoints of the suite that declare a permission: they are the way to
// reach the authorization of the API without touching a real controller.
[ApiController]
[Route("api/test/permissions")]
[Authorize]
public class PermissionTestController : ControllerBase
{
    [HttpGet("sales-read")]
    [Authorize(Policy = PermissionPolicies.SalesRead)]
    public IActionResult SalesRead() => Ok(new { permission = "ventas.read" });

    [HttpGet("inventory-read")]
    [Authorize(Policy = PermissionPolicies.InventoryRead)]
    public IActionResult InventoryRead() => Ok(new { permission = "inventario.read" });
}
