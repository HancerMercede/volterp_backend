using EitherWay;
using Volterp.Application.DTOs.NormalFiscalDocumentDtos;
using Volterp.Application.Exceptions.AppErrors;

namespace Volterp.Application.Interfaces;

public interface INormalFiscalDocumentService
{
    Task<Either<Error, NormalFiscalDocumentDto>> IssueAsync(
        Guid saleId, Guid companyId, Guid? userId = null, CancellationToken ct = default);
}
