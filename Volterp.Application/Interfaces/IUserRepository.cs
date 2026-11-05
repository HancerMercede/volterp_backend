using Volterp.Application.Helpers;
using Volterp.Domain.Entities;

namespace Volterp.Application.Interfaces;

public interface IUserRepository
{
    Task<User?> GetForAuthenticationAsync(string username, CancellationToken ct = default);
    Task<bool> ExistsByUsernameAsync(string username, CancellationToken ct = default);
    Task<User?> GetByUserByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetUserByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<User>> GetAllAsync(int pageNumber, int pageSize, CancellationToken ct = default);
    Task<User> AddUserAsync(User user, CancellationToken ct = default);
    Task UpdateUserAsync(User user, CancellationToken ct = default);
    Task DeleteUserAsync(Guid id, CancellationToken ct = default);
}
