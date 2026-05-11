using Volterp.Application.Helpers;
using Volterp.Domain.Entities;

namespace Volterp.Application.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default);
    Task<User?> GetByUserByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetUserByIdAsync(int id, CancellationToken ct = default);
    Task<PagedResult<User>> GetAllByCompanyAsync(int companyId,int pageNumber, int pageSize, CancellationToken ct = default);
    Task<User> AddUserAsync(User user, CancellationToken ct = default);
    Task UpdateUserAsync(User user, CancellationToken ct = default);
    Task DeleteUserAsync(int id, CancellationToken ct = default);
}