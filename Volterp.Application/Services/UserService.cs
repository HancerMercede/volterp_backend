using System.Runtime.InteropServices.ComTypes;
using EitherWay;
using Volterp.Application.DTOs.UserDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Exceptions.User;
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

    public async Task<Either<AppError, UserDto?>> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await EitherAsync
            .Try(() => unitOfWork.Users.GetUserByIdAsync(id, ct))
            .MapLeft(ex => new AppError(ex.Message))
            .Ensure(user=> user is not null, new AppError("user not found"))
            .Map(user => user?.MapTo<User, UserDto>())
            .Run();
    }

    public async Task<Either<AppError,UserDto>> CreateAsync(CreateUserDto request, CancellationToken ct = default)
    {
        return await EitherAsync.Try(() => unitOfWork.Users.GetByUsernameAsync(request.Username, ct))
            .MapLeft(ex => new AppError(ex.Message))
            .Ensure(user => user is not null, new AppError("username already exists"))
            .Map(user => request.Project())
            .Try(async user =>
            {
                user.PasswordHash = passwordHasher.Hash(request.Password);
                await unitOfWork.Users.AddUserAsync(user, ct);
                await unitOfWork.CommitAsync(ct);
                return user;
            }, exception => new AppError($"Failed to create user: {exception.Message}"))
            .Map(user => user.MapTo<User, UserDto>())
            .Run();
    }

    public async Task<UserDto> UpdateAsync(int id, UserWithPasswordHashDto request, CancellationToken ct = default)
    {
        var user = await unitOfWork.Users.GetUserByIdAsync(id, ct);
        
        if (user is null)
            throw new UserNotFoundException("User not found");

        user.Apply(x =>
        {
            x.Email = request.Email;
            x.FullName = request.FullName;
            x.Role = request.Role;
            x.IsActive = request.IsActive;
            x.UpdatedAt = DateTime.UtcNow;
        });
       

        await unitOfWork.Users.UpdateUserAsync(user, ct);
        await unitOfWork.CommitAsync(ct);

        return user.MapTo<User, UserDto>();
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var user = await unitOfWork.Users.GetUserByIdAsync(id, ct);
        
        if (user is null)
            throw new UserNotFoundException("User not found");
        
        await unitOfWork.Users.DeleteUserAsync(user.Id, ct);
        await unitOfWork.CommitAsync(ct);
    }

    public async Task<UserDto?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        var user = await unitOfWork.Users.GetByUserByEmailAsync(email, ct);
 
        if (user is null)
            throw new UserNotFoundException("User not found");
        
        return user.MapTo<User, UserDto>();
    }

    public async Task<UserWithPasswordHashDto?> GetByUsernameAsync(string username, CancellationToken ct = default)
    {
        var user = await unitOfWork.Users.GetByUsernameAsync(username, ct);
        
        if (user is null)
            return null;

        return user.MapTo<User, UserWithPasswordHashDto>();
    }
}