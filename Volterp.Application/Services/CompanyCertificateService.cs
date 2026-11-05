using EitherWay;
using Volterp.Application.DTOs.CompanyCertificateDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Domain.ValueObjects;

namespace Volterp.Application.Services;

public class CompanyCertificateService(IUnitOfWork unitOfWork, ICredentialProtector credentialProtector) : ICompanyCertificateService
{
    public async Task<PagedResult<CompanyCertificateDto>> GetAllAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var certificates = await unitOfWork.CompanyCertificates.GetAllAsync(_ => true, pageNumber, pageSize, ct);

        return certificates.MapTo<CompanyCertificate, CompanyCertificateDto>();
    }

    public async Task<Either<Error, CompanyCertificateDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await EitherAsync<Error, Guid>
            .FromRight(id)
            .Ensure(x => x != Guid.Empty, new Error("id must not be empty."))
            .FlatMap(async certificateId => await unitOfWork.CompanyCertificates.GetByCondictionsAsync(c => c.Id == certificateId, ct),
                ex => new Error(ex.Message))
            .Ensure(certificate => certificate is not null, new Error("Certificate not found."))
            .Map(certificate => certificate!.MapTo<CompanyCertificate, CompanyCertificateDto>())
            .Run();
    }

    public async Task<Either<Error, CompanyCertificateDto>> CreateAsync(CreateCompanyCertificateDto request, Guid companyId, Guid userId, CancellationToken ct = default)
    {
        var credential = request.Credential;

        // The credential stays opaque here on purpose: it is the encrypted
        // value of the issuer, and what it carries is decided by the host that
        // signs with it, not by the API that stores it.
        return await EitherAsync<Error, CreateCompanyCertificateDto>
            .FromRight(request)
            .Ensure(r => !string.IsNullOrWhiteSpace(r.Name), new Error("The name is required."))
            .Ensure(r => !string.IsNullOrWhiteSpace(r.Credential), new Error("The credential is required."))
            .Ensure(r => Rnc.TryParse(r.TaxId, out _), new Error("The tax id must be a valid RNC or citizen ID."))
            .Ensure(r => r.ExpiresAt > DateTime.UtcNow, new Error("The expiration date must be in the future."))
            .Map(r => r.Project())
            .Map(certificate =>
            {
                certificate.CompanyId = companyId;
                certificate.UpdateTaxId(new Rnc(certificate.TaxId));
                certificate.ExpiresAt = certificate.ExpiresAt.AsUtc();
                certificate.EncryptedCredential = credentialProtector.Protect(companyId, credential);
                certificate.SetCreatedBy(userId);
                return certificate;
            })
            .FlatMap(async certificate =>
            {
                await unitOfWork.CompanyCertificates.AddAsync(certificate, ct);
                await unitOfWork.CommitAsync(ct);
                return certificate;
            }, ex => new Error(ex.Message))
            .Map(certificate => certificate.MapTo<CompanyCertificate, CompanyCertificateDto>())
            .Run();
    }
}
