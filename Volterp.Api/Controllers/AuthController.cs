using EitherWay;
using Microsoft.AspNetCore.Mvc;
using Volterp.Api.Helpers;
using Volterp.Application.DTOs;
using Volterp.Application.DTOs.UserDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;

namespace Volterp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(IServiceManager serviceManager, IJwtService jwtService, IPasswordHasher passwordHasher) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] CreateUserDto request, CancellationToken ct)
    {
        var existingUser = await serviceManager.Users.GetByUsernameAsync(request.Username, ct);
        if (existingUser is Either<Error, UserWithPasswordHashDto>.Right)
            return BadRequest(new ErrorResponse("Username already exists", "The username is already in use."));

        var companyExists = await serviceManager.Companies.ExistsCompanyAsync(request.CompanyId, ct);
        if (!companyExists)
            return BadRequest(new ErrorResponse("Invalid company", "The specified company does not exist."));

        var userResult = await serviceManager.Users.CreateAsync(request, ct);
        if (userResult is Either<Error, UserDto>.Left err)
            return BadRequest(new ErrorResponse(err.Value.Message));

        var user = ((Either<Error, UserDto>.Right)userResult).Value;
        var token = jwtService.GenerateToken(user.Username, user.Email, user.Role.ToString(), user.CompanyId);

        return Ok(new LoginResponse(token, user.Username, user.Email, user.FullName, user.Role.ToString(), user.CompanyId));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var userResult = await serviceManager.Users.GetByUsernameAsync(request.Username, ct);
        if (userResult is Either<Error, UserWithPasswordHashDto>.Left)
            return Unauthorized(new ErrorResponse("Invalid credentials", "Check your credentials."));

        var user = ((Either<Error, UserWithPasswordHashDto>.Right)userResult).Value;

        if (!passwordHasher.Verify(request.Password, user.PasswordHash))
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