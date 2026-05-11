using Microsoft.AspNetCore.Mvc;
using Volterp.Api.Helpers;
using Volterp.Application.DTOs;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;

namespace Volterp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(IServiceManager serviceManager, IJwtService jwtService, IPasswordHasher passwordHasher) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<LoginResponse>> Register([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        var existingUser = await serviceManager.Users.GetByUsernameAsync(request.Username, ct);
        if (existingUser is not null)
            return BadRequest(new ErrorResponse("Username already exists", "The username is already in use."));

        var companyExists = await serviceManager.Companies.ExistsCompanyAsync(request.CompanyId, ct);
        if (!companyExists)
            return BadRequest(new ErrorResponse("Invalid company", "The specified company does not exist."));

        var user = await serviceManager.Users.CreateAsync(request, ct);

        var token = jwtService.GenerateToken(user.Username, user.Email, user.Role.ToString(), user.CompanyId);

        return Ok(new LoginResponse(token, user.Username, user.Email, user.FullName, user.Role.ToString(), user.CompanyId));
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var user = await serviceManager.Users.GetByUsernameAsync(request.Username, ct);

        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new ErrorResponse("Invalid credentials", "Check your credentials."));

        if (!user.IsActive)
            return Unauthorized(new ErrorResponse("User is inactive", "Your account is inactive."));

        if (!user.PasswordHash.StartsWith("$2"))
        {
            user.PasswordHash = passwordHasher.Hash(request.Password);
            await serviceManager.Users.UpdateAsync(user.Id, user, ct);
        }

        var token = jwtService.GenerateToken(user.Username, user.Email, user.Role.ToString(), user.CompanyId);

        return Ok(new LoginResponse(token, user.Username, user.Email, user.FullName, user.Role.ToString(), user.CompanyId));
    }
}