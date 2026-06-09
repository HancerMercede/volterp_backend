using EitherWay;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volterp.Api.Helpers;
using Volterp.Application.DTOs;
using Volterp.Application.DTOs.UserDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Interfaces;
using Volterp.Domain.Enums;

namespace Volterp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController(IServiceManager serviceManager, IPasswordHasher passwordHasher) : BaseController
{
    private bool CanManageUser(UserRole targetRole, UserRole? newRole = null)
    {
        if (IsSuperAdminOnly()) return true;
        if (IsAdmin())
        {
            if (targetRole == UserRole.SuperAdmin || targetRole == UserRole.Admin) return false;
            if (newRole == UserRole.SuperAdmin || newRole == UserRole.Admin) return false;
            return true;
        }
        return false;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<UserDto>>> GetUsers(CancellationToken ct, [FromQuery]PaginationParameters pagination)
    {
        if (!IsAdmin()) return Forbid();

        var users = await serviceManager.Users
            .GetAllAsync(GetCurrentUserCompanyId(),pagination.PageNumber, pagination.PageSize, ct);
        
        return Ok(users);
    }

    [HttpPost]
    public async Task<ActionResult<UserDto>> CreateUser([FromBody] CreateUserDto request, CancellationToken ct)
    {
        if (!IsAdmin()) return Forbid();

        // Only SuperAdmin can create Admin or SuperAdmin users
        if ((request.Role == UserRole.Admin || request.Role == UserRole.SuperAdmin) && !IsSuperAdminOnly())
            return Forbid();

        var companyId = GetCurrentUserCompanyId();

        var userForCreation = request with { CompanyId = companyId };

        var userResult = await serviceManager.Users.CreateAsync(userForCreation, ct);
        
        return userResult.Match<ActionResult<UserDto>>(
            error => BadRequest(error.Message),
            value => Ok(value));
        
        // if (userResult is Either<Error, UserDto>.Left err)
        //     return BadRequest(new ErrorResponse(err.Value.Message));
        //
        // var user = ((Either<Error, UserDto>.Right)userResult).Value;
        // return Created("", user);
    }

[HttpPut("{id}/role")]
    public async Task<ActionResult<UserDto>> UpdateUserRole(int id, [FromBody] UpdateUserRoleDto request, CancellationToken ct)
    {
        if (!IsAdmin()) return Forbid();

        var userResult = await serviceManager.Users.GetByIdAsync(id, ct);

        var user = ((Either<Error, UserDto>.Right)userResult).Value;

        if (!CanManageUser(user.Role, request.Role))
            return Forbid();

        var userForUpdate = new UserWithPasswordHashDto
        {
            Id = user.Id,
            Role = request.Role,
            Username = user.Username,
            Email = user.Email,
            FullName = user.FullName
        };

        var updateResult = await serviceManager.Users.UpdateAsync(id, userForUpdate, ct);
        
        return updateResult.Match<ActionResult<UserDto>>(
            error => BadRequest(error.Message),
            value =>  Ok(value));
        
        // if (updateResult is Either<Error, UserDto>.Left err)
        //     return BadRequest(new ErrorResponse(err.Value.Message));
        //
        // return Ok(((Either<Error, UserDto>.Right)updateResult).Value);
    }

    [HttpPut("{id}/status")]
    public async Task<ActionResult<UserDto>> UpdateUserStatus(int id, [FromBody] UpdateUserStatusDto request, CancellationToken ct)
    {
        if (!IsAdmin()) return Forbid();

        var userResult = await serviceManager.Users.GetByIdAsync(id, ct);
        
        var user = ((Either<Error, UserDto>.Right)userResult).Value;

        if ((user.Role == UserRole.Admin || user.Role == UserRole.SuperAdmin) && !IsSuperAdminOnly())
            return Forbid();

        var userForUpdate = new UserWithPasswordHashDto
        {
            Id = user.Id,
            Role = user.Role,
            Username = user.Username,
            Email = user.Email,
            FullName = user.FullName,
            IsActive = request.IsActive
        };

        var updateResult = await serviceManager.Users.UpdateAsync(id, userForUpdate, ct);
        
        return updateResult.Match<ActionResult<UserDto>>(
            error => BadRequest(error.Message), 
            value =>  Ok(value));
        
        
        // if (updateResult is Either<Error, UserDto>.Left err)
        //     return BadRequest(new ErrorResponse(err.Value.Message));
        //
        // return Ok(((Either<Error, UserDto>.Right)updateResult).Value);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(int id, CancellationToken ct)
    {
        if (!IsAdmin()) return Forbid();

        var userResult = await serviceManager.Users.GetByIdAsync(id, ct);
        
        var user = ((Either<Error, UserDto>.Right)userResult).Value;

        if ((user.Role == UserRole.Admin || user.Role == UserRole.SuperAdmin) && !IsSuperAdminOnly())
            return Forbid();

        var deleteResult = await serviceManager.Users.DeleteAsync(id, ct);
        
        return deleteResult.Match<IActionResult>(
                error => BadRequest(error.Message), 
                _=> NoContent() );
        
        // if (deleteResult is Either<Error, int>.Left err)
        //     return BadRequest(new ErrorResponse(err.Value.Message));

        // return NoContent();
    }
}