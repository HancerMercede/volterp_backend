using Volterp.Domain.Entities;

namespace Volterp.Application.Interfaces;

public interface IIssuedSeedRepository : IRepositoryBase<IssuedSeed>
{
    Task<IssuedSeed?> FindByValueAsync(string value, CancellationToken ct = default);

    // A seed is used once: the update only touches the seeds that are still
    // unused, so two validations of the same seed cannot both win.
    Task<bool> TryConsumeAsync(Guid id, DateTime usedAt, CancellationToken ct = default);

    Task<int> CommitAsync(CancellationToken ct = default);
}
