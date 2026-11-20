using EitherWay;
using Volterp.Application.DTOs.UserDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;

namespace Volterp.Application.Services;

public class UserService(IUnitOfWork unitOfWork, IPasswordHasher passwordHasher, ICurrentUserService currentUserService) : IUserService
{
    private const string CodeAlreadyInUseMessage = "The code is already in use.";
    private const string AtLeastOneRoleMessage = "A user needs at least one role.";

    public async Task<PagedResult<UserDto>> GetAllAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var users = await unitOfWork.Users.GetAllAsync(pageNumber, pageSize, ct);

        return users.MapTo<User, UserDto>();
    }

    public async Task<Either<Error, UserDto?>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await EitherAsync<Error, Guid>
            .FromRight(id)
            .Ensure(x => x != Guid.Empty, new Error("id must not be empty."))
            .FlatMap(async userId => await unitOfWork.Users.GetUserByIdAsync(userId, ct),
                ex => new Error(ex.Message))
            .Ensure(user => user is not null, new Error("user not found"))
            .Map(user => user?.MapTo<User, UserDto>())
            .Run();
    }

    public async Task<Either<Error,UserDto>> CreateAsync(CreateUserDto request, Guid companyId, Guid? userId, CancellationToken ct = default)
    {
        var requestedCode = request.Code?.Trim() ?? string.Empty;
        var requestedRoles = request.Roles ?? [];

        return await EitherAsync
            .Try(async() => await unitOfWork.Users.ExistsByUsernameAsync(request.Username, ct))
            .MapLeft(ex => new Error(ex.Message))
            .Ensure(exists => !exists, new Error("username already exists"))
            .Ensure(_ => requestedRoles.Length > 0, new Error(AtLeastOneRoleMessage))
            .Ensure(_ => !requestedRoles.Contains(UserRole.SuperAdmin) || currentUserService.IsSuperAdmin(),
                _ => new Error("Only SuperAdmin can create SuperAdmin users"))
            .FlatMap(async _ =>
            {
                // A user-provided code must be free inside the company; an empty one
                // means the per-company sequence provides the code.
                if (requestedCode.Length > 0)
                {
                    var codeInUse = await unitOfWork.Users.ExistsCodeAsync(companyId, requestedCode, null, ct);
                    if (codeInUse)
                        throw new Exception(CodeAlreadyInUseMessage);
                }
                return true;
            }, ex => new Error(ex.Message))
            .Map(_ => request.Project())
            .FlatMap(async user =>
            {
                user.CompanyId = companyId;
                user.PasswordHash = passwordHasher.Hash(request.Password);
                user.Code = requestedCode.Length > 0
                    ? requestedCode
                    : await unitOfWork.Codes.NextAsync(companyId, CodeEntityType.User, ct);

                // The roles are assigned once the company is known: the
                // assignment carries it and the tenant filter reads it.
                foreach (var role in requestedRoles)
                    user.AssignRole(role);

                if (userId.HasValue) user.SetCreatedBy(userId.Value);
                await unitOfWork.Users.AddUserAsync(user, ct);
                await unitOfWork.CommitAsync(ct);
                return user;
            }, exception => new Error($"Failed to create user: {exception.Message}"))
            .Map(user => user.MapTo<User, UserDto>())
            .Run();
    }

    public async Task<Either<Error, UserDto>> UpdateAsync(Guid id, UserWithPasswordHashDto request, Guid? userId, CancellationToken ct = default)
    {
        var requestedCode = request.Code?.Trim() ?? string.Empty;

        return await EitherAsync<Error, Guid>
            .FromRight(id)
            .Ensure(x=> x != Guid.Empty , new Error("id must not be empty."))
            .Ensure(_=> request.Email.IsValidEmail(),
                _ => new Error("the email address is not valid"))
            .FlatMap(async userId => await unitOfWork.Users.GetUserByIdAsync(userId, ct),
                ex => new Error(ex.Message))
            .Ensure(user => user is not null, new Error("user not found"))
            .FlatMap(async user =>
            {
                // An empty code keeps the current one; a provided code replaces it and
                // must be free inside the company (the row being updated is excluded).
                var current = user!;
                if (requestedCode.Length > 0)
                {
                    var codeInUse = await unitOfWork.Users.ExistsCodeAsync(current.CompanyId, requestedCode, current.Id, ct);
                    if (codeInUse)
                        throw new Exception(CodeAlreadyInUseMessage);
                }
                return current;
            }, ex => new Error(ex.Message))
            .Map(user =>
            {
                // The roles are not touched here: they have their own operation.
                return user.Apply(x =>
                {
                    x.Email = request.Email;
                    x.FullName = request.FullName;
                    x.IsActive = request.IsActive;
                    if (requestedCode.Length > 0) x.Code = requestedCode;
                    x.UpdatedAt = DateTime.UtcNow;
                    if (userId.HasValue) x.SetUpdatedBy(userId.Value);
                });
            })
            .FlatMap(async user =>
            {
                await unitOfWork.Users.UpdateUserAsync(user, ct);
                await unitOfWork.CommitAsync(ct);
                return user;
            }, ex => new Error($"Failed to update user: {ex.Message}"))
            .Map(user => user.MapTo<User, UserDto>())
            .Run();
    }

    // The roles of the user become the ones that were asked for: the ones that
    // are not there anymore are taken away and the new ones are added.
    public async Task<Either<Error, UserDto>> UpdateRolesAsync(Guid id, UpdateUserRolesDto request, Guid? userId, CancellationToken ct = default)
    {
        var requestedRoles = request.Roles ?? [];

        return await EitherAsync<Error, Guid>
            .FromRight(id)
            .Ensure(x => x != Guid.Empty, new Error("id must not be empty."))
            .Ensure(_ => requestedRoles.Length > 0, new Error(AtLeastOneRoleMessage))
            .FlatMap(async userId => await unitOfWork.Users.GetUserByIdAsync(userId, ct),
                ex => new Error(ex.Message))
            .Ensure(user => user is not null, new Error("user not found"))
            .FlatMap(async user =>
            {
                var current = user!;

                EnsureSuperAdminIsNotTouched(current, requestedRoles);

                ReconcileRoles(current, requestedRoles);

                if (userId.HasValue) current.SetUpdatedBy(userId.Value);

                await unitOfWork.Users.UpdateUserAsync(current, ct);
                await unitOfWork.CommitAsync(ct);
                return current;
            }, ex => new Error(ex.Message))
            .Map(user => user.MapTo<User, UserDto>())
            .Run();
    }

    public async Task<Either<Error, Unit>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        return await EitherAsync<Error, Guid>
            .FromRight(id)
            .Ensure(x=> x != Guid.Empty , new Error("id must not be empty."))
            .FlatMap(async userId => await unitOfWork.Users.GetUserByIdAsync(userId, ct),
                ex => new Error(ex.Message))
            .Ensure(user => user is not null, new Error("user not found"))
            .FlatMap(async user =>
            {
                await unitOfWork.Users.DeleteUserAsync(user.Id, ct);
                await unitOfWork.CommitAsync(ct);
                return new Unit();
            }, ex => new Error($"Failed to delete user: {ex.Message}"))
            .Run();
    }

    public async Task<Either<Error, UserDto?>> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        return await EitherAsync<Error, string>
            .FromRight(email)
            .Ensure(e => !string.IsNullOrWhiteSpace(e), new Error("email cannot be empty"))
            .FlatMap(async e => await unitOfWork.Users.GetByUserByEmailAsync(e, ct),
                ex => new Error(ex.Message))
            .Ensure(user => user is not null, new Error("user not found"))
            .Map(user => user?.MapTo<User, UserDto>())
            .Run();
    }

    public async Task<Either<Error, UserWithPasswordHashDto?>> GetForAuthenticationAsync(string username, CancellationToken ct = default)
    {
        return await EitherAsync<Error, string>
            .FromRight(username)
            .Ensure(u => !string.IsNullOrWhiteSpace(u), new Error("username cannot be empty"))
            .FlatMap(async u => await unitOfWork.Users.GetForAuthenticationAsync(u, ct),
                ex => new Error(ex.Message))
            .Ensure(user => user is not null, new Error("user not found"))
            .Map(user => user?.MapTo<User, UserWithPasswordHashDto>())
            .Run();
    }

    // The new roles are added first and the ones that are not asked for are
    // taken away afterwards: a user is never left without a role on the way.
    private static void ReconcileRoles(User user, UserRole[] requestedRoles)
    {
        var current = user.Roles.Select(assignment => assignment.Role).ToList();

        foreach (var role in requestedRoles.Where(role => !user.HasRole(role)))
            user.AssignRole(role);

        foreach (var role in current.Where(role => !requestedRoles.Contains(role)))
            user.RemoveRole(role);
    }

    // The role that administers the system is granted or taken away only by
    // somebody who holds it. The domain does not know who is asking, so the
    // actor decides here.
    private void EnsureSuperAdminIsNotTouched(User user, UserRole[] requestedRoles)
    {
        var touchesSuperAdmin = requestedRoles.Contains(UserRole.SuperAdmin)
            || user.Roles.Any(assignment => assignment.Role == UserRole.SuperAdmin);

        if (touchesSuperAdmin && !currentUserService.IsSuperAdmin())
            throw new InvalidOperationException("Only a SuperAdmin can grant or revoke the SuperAdmin role.");
    }
}
