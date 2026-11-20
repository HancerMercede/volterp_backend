using Volterp.Domain.Enums;

namespace Volterp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting(RateLimiterPolicies.Authentication)]
public class AuthController(IServiceManager serviceManager, IJwtService jwtService, IPasswordHasher passwordHasher)
    : BaseController
{
    // A machine that rents the API does not create users of the ERP: the
    // endpoint asks for the scheme of the user and refuses anything else.
    [HttpPost("register")]
    [Authorize(AuthenticationSchemes = "JwtBearer")]
    public async Task<IActionResult> Register([FromBody] CreateUserDto request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var companyId = GetCurrentUserCompanyId();
        var userResult = await serviceManager.Users.CreateAsync(request, companyId, userId, ct);

        return userResult.Match<IActionResult>(
            error => BadRequest(new ErrorResponse(error.Message)),
            user =>
            {
                var token = jwtService.GenerateToken(user.Username, user.Email, user.Roles, user.CompanyId, user.Id);
                var response = new LoginResponse(token, user.Username, user.Email, user.FullName, RoleNames(user.Roles), user.CompanyId);

                return Ok(response);
            });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var userResult = await serviceManager.Users.GetForAuthenticationAsync(request.Username, ct);

        return await userResult.Match<Task<IActionResult>>(
            _ => Task.FromResult<IActionResult>(
                Unauthorized(new ErrorResponse("Invalid credentials", "Check your credentials."))),
            user => HandleValidLogin(user, request, ct));
    }

    private async Task<IActionResult> HandleValidLogin(
        UserWithPasswordHashDto? user,
        LoginRequest request,
        CancellationToken ct)
    {
        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new ErrorResponse("Invalid credentials", "Check your credentials."));

        if (!user.IsActive)
            return Unauthorized(new ErrorResponse("User is inactive", "Your account is inactive."));

        // A user without roles has no permissions at all: the token would
        // carry none and the user would be locked out of the system.
        if (user.Roles.Length == 0)
            return Unauthorized(new ErrorResponse("User has no roles", "Your account has no role assigned."));

        if (!user.PasswordHash.StartsWith("$2"))
        {
            user.PasswordHash = passwordHasher.Hash(request.Password);
            var updateResult = await serviceManager.Users.UpdateAsync(user.Id, user, user.Id, ct);
            if (updateResult is Either<Error, UserDto>.Left)
                return BadRequest(new ErrorResponse("Update failed"));
        }

        var token = jwtService.GenerateToken(user.Username, user.Email, user.Roles, user.CompanyId, user.Id);
        return Ok(new LoginResponse(token, user.Username, user.Email, user.FullName, RoleNames(user.Roles), user.CompanyId));
    }

    private static string[] RoleNames(IEnumerable<UserRole> roles)
        => roles.Select(role => role.ToString()).ToArray();
}
