using Volterp.Application.DTOs;
using Volterp.Application.DTOs.UserDtos;
using Volterp.Application.Helpers;

namespace Volterp.Application.Interfaces;

public interface IUserService
{
    Task<PagedResult<UserDto>> GetAllAsync(int companyId, int page, int pageSize, CancellationToken ct = default);
    Task<UserDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<UserDto> CreateAsync(CreateUserDto request, CancellationToken ct = default);
    Task<UserDto> UpdateAsync(int id, UserWithPasswordHashDto request, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    Task<UserDto?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<UserWithPasswordHashDto?> GetByUsernameAsync(string username, CancellationToken ct = default);
    
}