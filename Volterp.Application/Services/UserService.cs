using Microsoft.EntityFrameworkCore;
using Volterp.Application.DTOs;
using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;

namespace Volterp.Application.Services;

public class UserService(IUnitOfWork unitOfWork, IPasswordHasher passwordHasher) : IUserService
{
    public async Task<PagedResult<UserDto>> GetAllAsync(int companyId, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var users = await unitOfWork.Users.GetAllByCompanyAsync(companyId, pageNumber, pageSize, ct);

        return users.Map(u =>
            new UserDto(u.Id, u.Username, u.Email, u.FullName, u.Role, u.IsActive, u.CompanyId));
    }

    public async Task<UserDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var user = await unitOfWork.Users.GetUserByIdAsync(id, ct);
        
        if (user is null)
            return null;

        return user.Map(u => new UserDto(
            u.Id, u.Username, u.Email, u.FullName,
            u.Role,  u.IsActive, u.CompanyId
        ));

    }

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        var existingUser = await unitOfWork.Users.GetByUsernameAsync(request.Username, ct);
        if (existingUser is not null)
            throw new ArgumentException("Username already exists");
        
        var hashedPassword = passwordHasher.Hash(request.Password);
        
         var user = request.Map(x=> 
             new User
        {
            Username = x.Username,
            PasswordHash = hashedPassword,
            Email = x.Email,
            FullName = x.FullName,
            Role = x.Role,
            CompanyId = x.CompanyId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });

        await unitOfWork.Users.AddUserAsync(user, ct);
        await unitOfWork.CommitAsync(ct);

        return user.Map(x=> new UserDto(
            x.Id, x.Username, x.Email, x.FullName,
            x.Role, x.IsActive, x.CompanyId));
    }

    public async Task<UserDto> UpdateAsync(int id, UserWithPasswordHashDto request, CancellationToken ct = default)
    {
        var user = await unitOfWork.Users.GetUserByIdAsync(id, ct);
        
        if (user is null)
            throw new ArgumentException("User not found");

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

        return new UserDto(
            user.Id, user.Username, user.Email, user.FullName,
            user.Role,user.IsActive, user.CompanyId
        );
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var user = await unitOfWork.Users.GetUserByIdAsync(id, ct);
        
        if (user is null)
            throw new ArgumentException("User not found");
        
        await unitOfWork.Users.DeleteUserAsync(user.Id, ct);
        await unitOfWork.CommitAsync(ct);
    }

    public async Task<UserDto?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        var user = await unitOfWork.Users.GetByUserByEmailAsync(email, ct);
 
        if (user is null)
            return null;
        
        return user.Map(x=> new UserDto(
            x.Id, x.Username, x.Email, x.FullName,
            x.Role, x.IsActive, x.CompanyId
        ));
    }

    public async Task<UserWithPasswordHashDto?> GetByUsernameAsync(string username, CancellationToken ct = default)
    {
        var user = await unitOfWork.Users.GetByUsernameAsync(username, ct);
        
        if (user is null)
            return null;
        
        return user.Map(x=> new UserWithPasswordHashDto{
          Id =  x.Id,
          Username = x.Username,
          Email = x.Email,
          FullName = x.FullName,
          Role = x.Role,
          IsActive = x.IsActive,
          CompanyId = x.CompanyId,
          PasswordHash  = x.PasswordHash
        });
    }
}