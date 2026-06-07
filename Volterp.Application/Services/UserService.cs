using Volterp.Application.DTOs.UserDtos;
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

    public async Task<UserDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var user = await unitOfWork.Users.GetUserByIdAsync(id, ct);
        
        if (user is null)
            throw new UserNotFoundException("User not found");

        return user.MapTo<User, UserDto>();

    }

    public async Task<UserDto> CreateAsync(CreateUserDto request, CancellationToken ct = default)
    {
        var existingUser = await unitOfWork.Users.GetByUsernameAsync(request.Username, ct);
        
        if (existingUser is not null)
            throw new UserAlreadyExistException("Username already exists");
        
        var hashedPassword = passwordHasher.Hash(request.Password);

        var user = request.Project();
        user.PasswordHash = hashedPassword;

        await unitOfWork.Users.AddUserAsync(user, ct);
        await unitOfWork.CommitAsync(ct);

        return user.MapTo<User, UserDto>();
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