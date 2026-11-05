using Microsoft.EntityFrameworkCore;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Infrastructure.Data;

namespace Volterp.Infrastructure.Repositories;

public class UserRepository(VolterpDbContext context)
    : RepositoryBase<User>(context), IUserRepository
{
    // Not tenant-scoped on purpose: authentication runs before the tenant claim exists.
    public async Task<User?> GetForAuthenticationAsync(string username, CancellationToken ct = default)
        => await Set()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == username, ct);

    // Not tenant-scoped: the unique index on Username is global.
    public async Task<bool> ExistsByUsernameAsync(string username, CancellationToken ct = default)
        => await Set()
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Username == username, ct);

    public async Task<User?> GetByUserByEmailAsync(string email, CancellationToken ct = default)
        =>  await GetByCondictionsAsync(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase), ct);

    public async Task<User?> GetUserByIdAsync(Guid id, CancellationToken ct = default)
        => await GetByIdAsync(id, ct);

    public async Task<PagedResult<User>> GetAllAsync(int pageNumber,
        int pageSize, CancellationToken ct = default)
        => await GetAllAsync(_=>true,pageNumber, pageSize, ct);


    public async Task<User> AddUserAsync(User user, CancellationToken ct = default)
    {
        await AddAsync(user, ct);
        return user;
    }

    public async Task UpdateUserAsync(User user, CancellationToken ct = default)
        => await UpdateAsync(user, ct);

    public async Task DeleteUserAsync(Guid id, CancellationToken ct = default)
    {
        var user = await GetByIdAsync(id, ct);
        if (user is not null)
            await DeleteAsync(user, ct);
    }
}
