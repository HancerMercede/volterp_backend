using EitherWay;
using Volterp.Application.DTOs;
using Volterp.Application.DTOs.UserDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Helpers;

namespace Volterp.Application.Interfaces;

public interface IUserService
{
    Task<PagedResult<UserDto>> GetAllAsync(int companyId, int page, int pageSize, CancellationToken ct = default);
    Task<Either<Error, UserDto>> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Either<Error,UserDto>> CreateAsync(CreateUserDto request, CancellationToken ct = default);
    Task<Either<Error, UserDto>> UpdateAsync(int id, UserWithPasswordHashDto request, CancellationToken ct = default);
    Task<Either<Error, int>> DeleteAsync(int id, CancellationToken ct = default);
    Task<Either<Error, UserDto>> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<Either<Error, UserWithPasswordHashDto>> GetByUsernameAsync(string username, CancellationToken ct = default);
    
}