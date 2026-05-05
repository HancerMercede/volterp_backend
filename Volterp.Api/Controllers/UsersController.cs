using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volterp.Application.DTOs;
using Volterp.Application.Interfaces;

namespace Volterp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController(IUnitOfWork unitOfWork, IPasswordHasher passwordHasher) : BaseController
{
    private bool IsAdmin()
        => User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value.ToLower() == "admin";

    [HttpGet]
    public async Task<ActionResult<List<UserDto>>> GetUsers(CancellationToken ct)
    {
        if (!IsAdmin()) return Forbid();

        var users = await unitOfWork.Users.GetAllByCompanyAsync(GetCurrentUserCompanyId(), ct);
        return Ok(users.Select(u => new UserDto(u.Id, u.Username, u.Email, u.FullName, u.Role, u.IsActive, u.CompanyId)).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<UserDto>> CreateUser([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        if (!IsAdmin()) return Forbid();

        var companyId = GetCurrentUserCompanyId();
        if (await unitOfWork.Users.GetByUsernameAsync(request.Username, ct) is not null)
            return BadRequest(new ErrorResponse("Username already exists"));

        var user = new Domain.Entities.User
        {
            Username = request.Username,
            PasswordHash = passwordHasher.Hash(request.Password),
            Email = request.Email,
            FullName = request.FullName,
            Role = request.Role,
            CompanyId = companyId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await unitOfWork.Users.AddUserAsync(user, ct);
        await unitOfWork.CommitAsync(ct);

        return Created("", new UserDto(user.Id, user.Username, user.Email, user.FullName, user.Role, user.IsActive, user.CompanyId));
    }

    [HttpPut("{id}/role")]
    public async Task<ActionResult<UserDto>> UpdateUserRole(int id, [FromBody] UpdateUserRoleRequest request, CancellationToken ct)
    {
        if (!IsAdmin()) return Forbid();

        var user = await unitOfWork.Users.GetUserByIdAsync(id, ct);
        if (user is null) return NotFound(new ErrorResponse("User not found"));

        user.Role = request.Role;
        await unitOfWork.Users.UpdateUserAsync(user, ct);
        await unitOfWork.CommitAsync(ct);

        return Ok(new UserDto(user.Id, user.Username, user.Email, user.FullName, user.Role, user.IsActive, user.CompanyId));
    }

    [HttpPut("{id}/status")]
    public async Task<ActionResult<UserDto>> UpdateUserStatus(int id, [FromBody] UpdateUserStatusRequest request, CancellationToken ct)
    {
        if (!IsAdmin()) return Forbid();

        var user = await unitOfWork.Users.GetUserByIdAsync(id, ct);
        if (user is null) return NotFound(new ErrorResponse("User not found"));

        user.IsActive = request.IsActive;
        await unitOfWork.Users.UpdateUserAsync(user, ct);
        await unitOfWork.CommitAsync(ct);

        return Ok(new UserDto(user.Id, user.Username, user.Email, user.FullName, user.Role, user.IsActive, user.CompanyId));
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteUser(int id, CancellationToken ct)
    {
        if (!IsAdmin()) return Forbid();

        var user = await unitOfWork.Users.GetUserByIdAsync(id, ct);
        if (user is null) return NotFound(new ErrorResponse("User not found"));
        if (user.Role == "Admin") return BadRequest(new ErrorResponse("Cannot delete an admin user"));

        await unitOfWork.Users.DeleteUserAsync(id, ct);
        await unitOfWork.CommitAsync(ct);

        return NoContent();
    }
}