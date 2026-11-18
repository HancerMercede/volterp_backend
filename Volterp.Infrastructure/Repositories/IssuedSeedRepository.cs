using Microsoft.EntityFrameworkCore;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Infrastructure.Data;

namespace Volterp.Infrastructure.Repositories;

public class IssuedSeedRepository(VolterpDbContext context)
    : RepositoryBase<IssuedSeed>(context), IIssuedSeedRepository
{
    public async Task<IssuedSeed?> FindByValueAsync(string value, CancellationToken ct = default)
        => await Set()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Value == value, ct);

    public async Task<bool> TryConsumeAsync(Guid id, DateTime usedAt, CancellationToken ct = default)
        => await Set()
            .Where(s => s.Id == id && s.UsedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.UsedAt, usedAt), ct) == 1;

    public async Task<int> CommitAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
