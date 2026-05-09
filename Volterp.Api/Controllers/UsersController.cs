using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volterp.Api.Helpers;
using Volterp.Application.DTOs;
using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;

namespace Volterp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController(IServiceManager serviceManager, IPasswordHasher passwordHasher) : BaseController
{
    private bool IsAdmin()
        => User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value.ToLower() == "admin";

    [HttpGet]
    public async Task<ActionResult<List<UserDto>>> GetUsers(CancellationToken ct, [FromQuery]PaginationParameters pagination)
    {
        if (!IsAdmin()) return Forbid();

        var users = await serviceManager.Users.GetAllAsync(GetCurrentUserCompanyId(),pagination.PageNumber, pagination.PageSize, ct);
        return Ok(users);
    }

    [HttpPost]
    public async Task<ActionResult<UserDto>> CreateUser([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        if (!IsAdmin()) return Forbid();

        var companyId = GetCurrentUserCompanyId();
        
        if (await serviceManager.Users.GetByUsernameAsync(request.Username, ct) is not null)
            return BadRequest(new ErrorResponse("Username already exists"));

        var userForCreation = request with
        {
            CompanyId = companyId
        };
        
       var user = await serviceManager.Users.CreateAsync(userForCreation, ct);
       
       return Created("", user);
    }

    [HttpPut("{id}/role")]
    public async Task<ActionResult<UserDto>> UpdateUserRole(int id, [FromBody] UpdateUserRoleRequest request, CancellationToken ct)
    {
        if (!IsAdmin()) return Forbid();

        var user = await serviceManager.Users.GetByIdAsync(id, ct);
        if (user is null) return NotFound(new ErrorResponse("User not found"));
        
        var userWithNewRole = user.Apply(r => r with { Role = request.Role });

        var userForUpdate = userWithNewRole.Map(u => new UserWithPasswordHashDto
        {
            Id =  u.Id,
            Role =  u.Role,
            Username = u.Username,
            Email = u.Email,
            FullName = u.FullName
        });
       
        var userDto =  await serviceManager.Users.UpdateAsync(user.Id, userForUpdate, ct);
        
        return Ok(userDto);
    }

    [HttpPut("{id}/status")]
    public async Task<ActionResult<UserDto>> UpdateUserStatus(int id, [FromBody] UpdateUserStatusRequest request, CancellationToken ct)
    {
        if (!IsAdmin()) return Forbid();

        var user = await serviceManager.Users.GetByIdAsync(id, ct);
        if (user is null) return NotFound(new ErrorResponse("User not found"));

        var userWithNewStatus = user.Apply(r => r with { IsActive = request.IsActive });

        var userForUpdate = userWithNewStatus.Map(u => new UserWithPasswordHashDto
        {
            Id = u.Id,
            Role = u.Role,
            Username = u.Username,
            Email = u.Email,
            FullName = u.FullName
        });
        
        var userDto = await serviceManager.Users.UpdateAsync(user.Id, userForUpdate, ct);
        
        return Ok(userDto);
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteUser(int id, CancellationToken ct)
    {
        if (!IsAdmin()) return Forbid();

        var user = await serviceManager.Users.GetByIdAsync(id, ct);
        if (user is null) return NotFound(new ErrorResponse("User not found"));
        if (user.Role == "Admin") return BadRequest(new ErrorResponse("Cannot delete an admin user"));

        await serviceManager.Users.DeleteAsync(id, ct);
        return NoContent();
    }
}