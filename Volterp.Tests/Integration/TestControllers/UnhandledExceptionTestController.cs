using Microsoft.AspNetCore.Mvc;

namespace Volterp.Tests.Integration.TestControllers;

/// <summary>
/// The suite needs to reach the branch of the middleware where an exception
/// nobody handled leaves the API (the 5xx of the error envelope). No endpoint
/// of the ERP fails that way on purpose, so the tests bring their own.
/// </summary>
[ApiController]
[Route("test/unhandled")]
public class UnhandledExceptionTestController : ControllerBase
{
    [HttpGet("throw")]
    public IActionResult Throw() => throw new InvalidOperationException("The test threw this.");
}
