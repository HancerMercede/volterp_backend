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

    // The code is unique inside a company, so the check is scoped by company.
    public async Task<bool> ExistsCodeAsync(Guid companyId, string code, Guid? excludeId = null, CancellationToken ct = default)
    {
        if (excludeId.HasValue)
            return await ExistsAsync(u => u.CompanyId == companyId && u.Code == code && u.Id != excludeId.Value, ct);

        return await ExistsAsync(u => u.CompanyId == companyId && u.Code == code, ct);
    }

    // The email of a user is compared without case: the lookup finds the user
    // whatever the case of the address it is given. The comparison is the one
    // both providers translate into SQL.
    //
    // The unique index of the column is case-sensitive in Postgres, so the
    // database would take two users whose addresses differ only in the case.
    // That is why the check of the duplicate address goes apart: the command
    // refuses an address that only differs in the case of one that is already
    // in use.
    public async Task<User?> GetByUserByEmailAsync(string email, CancellationToken ct = default)
        =>  await GetByCondictionsAsync(u => u.Email.ToLower() == email.ToLowerInvariant(), ct);

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
