using Microsoft.AspNetCore.Mvc;
using Volterp.Application.DTOs;
using Volterp.Application.Interfaces;

namespace Volterp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(IUnitOfWork unitOfWork, IJwtService jwtService, IPasswordHasher passwordHasher) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<LoginResponse>> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var existingUser = await unitOfWork.Users.GetByUsernameAsync(request.Username, ct);
        if (existingUser is not null)
            return BadRequest(new ErrorResponse("Username already exists", "The username is already in use."));

        var companyExists = await unitOfWork.Companies.ExistsAsync(c=>c.Id == request.CompanyId, ct);
        if (!companyExists)
            return BadRequest(new ErrorResponse("Invalid company", "The specified company does not exist."));

        var user = new Domain.Entities.User
        {
            Username = request.Username,
            PasswordHash = passwordHasher.Hash(request.Password),
            Email = request.Email,
            FullName = request.FullName,
            Role = "User",
            CompanyId = request.CompanyId
        };

        await unitOfWork.Users.AddUserAsync(user, ct);
        await unitOfWork.CommitAsync(ct);

        var token = jwtService.GenerateToken(user.Username, user.Email, user.Role, user.CompanyId);

        return Ok(new LoginResponse(token, user.Username, user.Email, user.FullName, user.Role, user.CompanyId));
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var user = await unitOfWork.Users.GetByUsernameAsync(request.Username, ct);

        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new ErrorResponse("Invalid credentials", "Check your credentials."));

        if (!user.IsActive)
            return Unauthorized(new ErrorResponse("User is inactive", "Your account is inactive."));

        if (!user.PasswordHash.StartsWith("$2"))
        {
            user.PasswordHash = passwordHasher.Hash(request.Password);
            await unitOfWork.Users.UpdateUserAsync(user, ct);
        }

        var token = jwtService.GenerateToken(user.Username, user.Email, user.Role, user.CompanyId);

        return Ok(new LoginResponse(token, user.Username, user.Email, user.FullName, user.Role, user.CompanyId));
    }
}