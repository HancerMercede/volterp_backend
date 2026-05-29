
using Microsoft.VisualBasic;
using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Infrastructure.Data;

namespace Volterp.Infrastructure.Repositories;

public class UserRepository(VolterpDbContext context)
    : RepositoryBase<User>(context), IUserRepository
{
    public async Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default)
        => await GetByCondictionsAsync(u => u.Username == username, ct);

    public async Task<User?> GetByUserByEmailAsync(string email, CancellationToken ct = default)
        =>  await GetByCondictionsAsync(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase), ct);

    public new async Task<User?> GetUserByIdAsync(int id, CancellationToken ct = default)
        => await GetByIdAsync(id, ct);

    public async Task<PagedResult<User>> GetAllByCompanyAsync(int companyId, int pageNumber, 
        int pageSize, CancellationToken ct = default)
        => await GetAllAsync(c=>c.CompanyId == companyId,pageNumber, pageSize, ct);
        

    public async Task<User> AddUserAsync(User user, CancellationToken ct = default)
    {
        await AddAsync(user, ct);
        return user;
    }

    public async Task UpdateUserAsync(User user, CancellationToken ct = default)
        => await UpdateAsync(user, ct);

    public async Task DeleteUserAsync(int id, CancellationToken ct = default)
    {
        var user = await GetByIdAsync(id, ct);
        if (user is not null)
            await DeleteAsync(user, ct);
    }
}