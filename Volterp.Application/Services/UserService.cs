using EitherWay;
using Volterp.Application.DTOs.UserDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;

namespace Volterp.Application.Services;

public class UserService(IUnitOfWork unitOfWork, IPasswordHasher passwordHasher) : IUserService
{
    public async Task<PagedResult<UserDto>> GetAllAsync(int companyId, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var users = await unitOfWork.Users.GetAllByCompanyAsync(companyId, pageNumber, pageSize, ct);

        return users.MapTo<User, UserDto>();
    }

    public async Task<Either<Error, UserDto?>> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await EitherAsync<Error, int>
            .FromRight(id)
            .Ensure(x => x > 0, new Error("id must be greater than zero."))
            .FlatMap(async userId => await unitOfWork.Users.GetUserByIdAsync(userId, ct),
                ex => new Error(ex.Message))
            .Ensure(user => user is not null, new Error("user not found"))
            .Map(user => user?.MapTo<User, UserDto>())
            .Run();
    }

    public async Task<Either<Error,UserDto>> CreateAsync(CreateUserDto request, CancellationToken ct = default)
    {
        return await EitherAsync
            .Try(async() => await unitOfWork.Users.GetByUsernameAsync(request.Username, ct))
            .MapLeft(ex => new Error(ex.Message))
            .Ensure(user => user is null, new Error("username already exists"))
            .Map(_ => request.Project())
            .FlatMap(async user =>
            {
                user.PasswordHash = passwordHasher.Hash(request.Password);
                await unitOfWork.Users.AddUserAsync(user, ct);
                await unitOfWork.CommitAsync(ct);
                return user;
            }, exception => new Error($"Failed to create user: {exception.Message}"))
            .Map(user => user.MapTo<User, UserDto>())
            .Run();
    }

    public async Task<Either<Error, UserDto>> UpdateAsync(int id, UserWithPasswordHashDto request, CancellationToken ct = default)
    {
        return await EitherAsync<Error, int>
            .FromRight(id)
            .Ensure(x=> x > 0 , new Error("id must be greater than zero."))
            .FlatMap(async userId => await unitOfWork.Users.GetUserByIdAsync(userId, ct),
                ex => new Error(ex.Message))
            .Ensure(user => user is not null, new Error("user not found"))
            .Map(user =>
            {
                user.Apply(x =>
                {
                    x.Email = request.Email;
                    x.FullName = request.FullName;
                    x.Role = request.Role;
                    x.IsActive = request.IsActive;
                    x.UpdatedAt = DateTime.UtcNow;
                });
                return user;
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

    public async Task<Either<Error, Unit>> DeleteAsync(int id, CancellationToken ct = default)
    {
        return await EitherAsync<Error, int>
            .FromRight(id)
            .Ensure(x=> x > 0, new Error("id must be greater than zero."))
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

    public async Task<Either<Error, UserWithPasswordHashDto?>> GetByUsernameAsync(string username, CancellationToken ct = default)
    {
        return await EitherAsync<Error, string>
            .FromRight(username)
            .Ensure(u => !string.IsNullOrWhiteSpace(u), new Error("username cannot be empty"))
            .FlatMap(async u => await unitOfWork.Users.GetByUsernameAsync(u, ct),
                ex => new Error(ex.Message))
            .Ensure(user => user is not null, new Error("user not found"))
            .Map(user => user?.MapTo<User, UserWithPasswordHashDto>())
            .Run();
    }
}
