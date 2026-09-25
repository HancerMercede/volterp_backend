using EitherWay;
using Volterp.Application.DTOs;
using Volterp.Application.DTOs.UserDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Helpers;

namespace Volterp.Application.Interfaces;

public interface IUserService
{
    Task<PagedResult<UserDto>> GetAllAsync(int page, int pageSize, CancellationToken ct = default);
    Task<Either<Error, UserDto?>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Either<Error,UserDto>> CreateAsync(CreateUserDto request, Guid companyId, Guid? userId, CancellationToken ct = default);
    Task<Either<Error, UserDto>> UpdateAsync(Guid id, UserWithPasswordHashDto request, Guid? userId, CancellationToken ct = default);
    Task<Either<Error, Unit>> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<Either<Error, UserDto?>> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<Either<Error, UserWithPasswordHashDto?>> GetByUsernameAsync(string username, CancellationToken ct = default);

}
