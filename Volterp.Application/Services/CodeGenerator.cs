using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;

namespace Volterp.Application.Services;

public class CodeGenerator(ICodeSequenceRepository sequences) : ICodeGenerator
{
    public async Task<string> NextAsync(Guid companyId, CodeEntityType entityType, CancellationToken ct = default)
    {
        var value = await sequences.NextValueAsync(companyId, entityType, ct);

        return CodeFormat.Build(entityType, value);
    }
}
