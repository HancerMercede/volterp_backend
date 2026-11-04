using EitherWay;
using Volterp.Application.DTOs.CompanyCertificateDtos;
using Volterp.Application.Exceptions.AppErrors;

namespace Volterp.Application.Interfaces;

public interface ICompanyCertificateService
{
    Task<PagedResult<CompanyCertificateDto>> GetAllAsync(int pageNumber, int pageSize, CancellationToken ct = default);
    Task<Either<Error, CompanyCertificateDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Either<Error, CompanyCertificateDto>> CreateAsync(CreateCompanyCertificateDto request, Guid companyId, Guid userId, CancellationToken ct = default);
}
