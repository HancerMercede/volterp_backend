using Volterp.Domain.Enums;

namespace Volterp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController(IServiceManager serviceManager, IPasswordHasher passwordHasher) : BaseController
{
    // The roles of a user are managed by an admin. The role that administers
    // the system is managed only by a super admin, and the same goes for the
    // users that hold it.
    private bool CanManageRoles(IReadOnlyCollection<UserRole> currentRoles, IReadOnlyCollection<UserRole> requestedRoles)
    {
        if (IsSuperAdminOnly()) return true;
        if (IsAdmin())
        {
            if (currentRoles.Contains(UserRole.SuperAdmin)) return false;
            if (requestedRoles.Contains(UserRole.SuperAdmin)) return false;
            return true;
        }
        return false;
    }

    private static bool HoldsAdminRole(IReadOnlyCollection<UserRole> roles)
        => roles.Contains(UserRole.Admin) || roles.Contains(UserRole.SuperAdmin);

    [HttpGet]
    public async Task<ActionResult<PagedResult<UserDto>>> GetUsers(CancellationToken ct, [FromQuery]PaginationParameters pagination)
    {
        if (!IsAdmin()) return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("You do not have permission to perform this action."));

        EnsureTenant();
        var users = await serviceManager.Users
            .GetAllAsync(pagination.PageNumber, pagination.PageSize, ct);

        return Ok(users);
    }

    [HttpPost]
    public async Task<ActionResult<UserDto>> CreateUser([FromBody] CreateUserDto request, CancellationToken ct)
    {
        if (!IsAdmin()) return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("You do not have permission to perform this action."));

        var requestedRoles = request.Roles ?? [];

        if (HoldsAdminRole(requestedRoles) && !IsSuperAdminOnly())
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("You do not have permission to perform this action."));

        var companyId = GetCurrentUserCompanyId();
        var userId = GetCurrentUserId();

        var userResult = await serviceManager.Users.CreateAsync(request, companyId, userId, ct);

        return userResult.Match<ActionResult<UserDto>>(
            error => BadRequest(new ErrorResponse(error.Message)),
            value => Ok(value));

    }

    // The roles of the user become the ones that are asked for: the ones that
    // are not there anymore are taken away and the new ones are added.
    [HttpPut("{id}/roles")]
    public async Task<ActionResult<UserDto>> UpdateUserRoles(Guid id, [FromBody] UpdateUserRolesDto request, CancellationToken ct)
    {
        if (!IsAdmin()) return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("You do not have permission to perform this action."));

        var requestedRoles = request.Roles ?? [];

        if (requestedRoles.Length == 0)
            return BadRequest(new ErrorResponse("A user needs at least one role."));

        var userId = GetCurrentUserId();
        var userResult = await serviceManager.Users.GetByIdAsync(id, ct);

        var user = userResult.Match(
            error => (UserDto?)null,
            value => value);

        if (user is null)
            return NotFound(new ErrorResponse("The user does not exist."));

        if (!CanManageRoles(user.Roles, requestedRoles))
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("You do not have permission to perform this action."));

        var updateResult = await serviceManager.Users.UpdateRolesAsync(id, request, userId, ct);

        return updateResult.Match<ActionResult<UserDto>>(
            error => BadRequest(new ErrorResponse(error.Message)),
            value => Ok(value));
    }

    [HttpPut("{id}/status")]
    public async Task<ActionResult<UserDto>> UpdateUserStatus(Guid id, [FromBody] UpdateUserStatusDto request, CancellationToken ct)
    {
        if (!IsAdmin()) return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("You do not have permission to perform this action."));

        var userId = GetCurrentUserId();
        var userResult = await serviceManager.Users.GetByIdAsync(id, ct);

        var user = ((Either<Error, UserDto>.Right)userResult!).Value;

        if (HoldsAdminRole(user.Roles) && !IsSuperAdminOnly())
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("You do not have permission to perform this action."));

        var userForUpdate = new UserWithPasswordHashDto
        {
            Id = user.Id,
            Roles = user.Roles,
            Username = user.Username,
            Email = user.Email,
            FullName = user.FullName,
            IsActive = request.IsActive
        };

        var updateResult = await serviceManager.Users.UpdateAsync(id, userForUpdate, userId, ct);

        return updateResult.Match<ActionResult<UserDto>>(
            error => BadRequest(new ErrorResponse(error.Message)),
            value => Ok(value));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(Guid id, CancellationToken ct)
    {
        if (!IsAdmin()) return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("You do not have permission to perform this action."));

        var userResult = await serviceManager.Users.GetByIdAsync(id, ct);

        var user = ((Either<Error, UserDto>.Right)userResult!).Value;

        if (HoldsAdminRole(user.Roles) && !IsSuperAdminOnly())
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("You do not have permission to perform this action."));

        var deleteResult = await serviceManager.Users.DeleteAsync(id, ct);

        return deleteResult.Match<IActionResult>(
                error => BadRequest(new ErrorResponse(error.Message)),
                _ => NoContent());

    }
}
