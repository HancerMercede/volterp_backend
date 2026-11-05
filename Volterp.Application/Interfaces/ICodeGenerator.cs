using Volterp.Domain.Enums;

namespace Volterp.Application.Interfaces;

public interface ICodeGenerator
{
    Task<string> NextAsync(Guid companyId, CodeEntityType entityType, CancellationToken ct = default);
}
