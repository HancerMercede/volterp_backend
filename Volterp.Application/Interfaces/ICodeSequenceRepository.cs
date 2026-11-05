using Volterp.Domain.Enums;

namespace Volterp.Application.Interfaces;

public interface ICodeSequenceRepository
{
    Task<int> NextValueAsync(Guid companyId, CodeEntityType entityType, CancellationToken ct = default);
}
