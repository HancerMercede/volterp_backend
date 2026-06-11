using EitherWay;
using Microsoft.AspNetCore.Mvc;
using Volterp.Api.Helpers;
using Volterp.Application.DTOs;
using Volterp.Application.DTOs.UserDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Interfaces;

namespace Volterp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(IServiceManager serviceManager, IJwtService jwtService, IPasswordHasher passwordHasher) : BaseController
{
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] CreateUserDto request, CancellationToken ct)
    {
        var userResult = await serviceManager.Users.CreateAsync(request, ct);

        return userResult.Match<IActionResult>(
            error => BadRequest(new ErrorResponse(error.Message)),
            user =>
            {
                var token = jwtService.GenerateToken(user.Username, user.Email, user.Role.ToString(), user.CompanyId);
                return Ok(new LoginResponse(token, user.Username, user.Email, user.FullName, user.Role.ToString(),
                    user.CompanyId));
            });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var userResult = await serviceManager.Users.GetByUsernameAsync(request.Username, ct);

        return await userResult.Match<Task<IActionResult>>(
            _ => 
                Task.FromResult<IActionResult>(
                Unauthorized(new ErrorResponse("Invalid credentials", "Check your credentials."))),
            user => HandleValidLogin(user, request, ct));
    }

    private async Task<IActionResult> HandleValidLogin(
        UserWithPasswordHashDto? user,
        LoginRequest request, CancellationToken ct)
    {
        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new ErrorResponse("Invalid credentials", "Check your credentials."));

        if (!user.IsActive)
            return Unauthorized(new ErrorResponse("User is inactive", "Your account is inactive."));

        if (!user.PasswordHash.StartsWith("$2"))
        {
            user.PasswordHash = passwordHasher.Hash(request.Password);
            var updateResult = await serviceManager.Users.UpdateAsync(user.Id, user, ct);
            if (updateResult is Either<Error, UserDto>.Left)
                return BadRequest(new ErrorResponse("Update failed"));
        }

        var token = jwtService.GenerateToken(user.Username, user.Email, user.Role.ToString(), user.CompanyId);
        return Ok(new LoginResponse(token, user.Username, user.Email, user.FullName, user.Role.ToString(), user.CompanyId));
    }
}
