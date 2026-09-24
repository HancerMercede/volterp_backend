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
            .GetAllAsync(GetCurrentUserCompanyId(), pagination.PageNumber, pagination.PageSize, ct);

        return Ok(users);
    }

    [HttpPost]
    public async Task<ActionResult<UserDto>> CreateUser([FromBody] CreateUserDto request, CancellationToken ct)
    {
        if (!IsAdmin()) return Forbid();

        if ((request.Role == UserRole.Admin || request.Role == UserRole.SuperAdmin) && !IsSuperAdminOnly())
            return Forbid();

        var companyId = GetCurrentUserCompanyId();
        var userId = GetCurrentUserId();

        var userResult = await serviceManager.Users.CreateAsync(request, companyId, userId, ct);

        return userResult.Match<ActionResult<UserDto>>(
            error => BadRequest(error.Message),
            value => Ok(value));

    }

    [HttpPut("{id}/role")]
    public async Task<ActionResult<UserDto>> UpdateUserRole(Guid id, [FromBody] UpdateUserRoleDto request, CancellationToken ct)
    {
        if (!IsAdmin()) return Forbid();

        var userId = GetCurrentUserId();
        var userResult = await serviceManager.Users.GetByIdAsync(id, ct);

        var user = ((Either<Error, UserDto>.Right)userResult!).Value;

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

        var updateResult = await serviceManager.Users.UpdateAsync(id, userForUpdate, userId, ct);

        return updateResult.Match<ActionResult<UserDto>>(
            error => BadRequest(error.Message),
            value => Ok(value));
    }

    [HttpPut("{id}/status")]
    public async Task<ActionResult<UserDto>> UpdateUserStatus(Guid id, [FromBody] UpdateUserStatusDto request, CancellationToken ct)
    {
        if (!IsAdmin()) return Forbid();

        var userId = GetCurrentUserId();
        var userResult = await serviceManager.Users.GetByIdAsync(id, ct);

        var user = ((Either<Error, UserDto>.Right)userResult!).Value;

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

        var updateResult = await serviceManager.Users.UpdateAsync(id, userForUpdate, userId, ct);

        return updateResult.Match<ActionResult<UserDto>>(
            error => BadRequest(error.Message),
            value => Ok(value));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(Guid id, CancellationToken ct)
    {
        if (!IsAdmin()) return Forbid();

        var userResult = await serviceManager.Users.GetByIdAsync(id, ct);

        var user = ((Either<Error, UserDto>.Right)userResult!).Value;

        if ((user.Role == UserRole.Admin || user.Role == UserRole.SuperAdmin) && !IsSuperAdminOnly())
            return Forbid();

        var deleteResult = await serviceManager.Users.DeleteAsync(id, ct);

        return deleteResult.Match<IActionResult>(
                error => BadRequest(error.Message),
                _ => NoContent());

    }
}