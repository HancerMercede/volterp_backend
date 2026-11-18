using EitherWay;
using Volterp.Application.DTOs.NormalFiscalSequenceDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;

namespace Volterp.Application.Services;

// The range the DGII authorized to the taxpayer for the normal (paper)
// documents: the same rules as the electronic one, plus the two the number of
// a normal document imposes.
public class NormalFiscalSequenceService(IUnitOfWork unitOfWork) : INormalFiscalSequenceService
{
    public async Task<PagedResult<NormalFiscalSequenceDto>> GetAllAsync(
        Guid companyId, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var ranges = await unitOfWork.NormalFiscalSequences.GetAllAsync(
            range => range.CompanyId == companyId, pageNumber, pageSize, ct);

        return ranges.MapTo<NormalFiscalSequence, NormalFiscalSequenceDto>();
    }

    public async Task<Either<Error, NormalFiscalSequenceDto>> GetByIdAsync(
        Guid id, Guid companyId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, Guid>
            .FromRight(id)
            .Ensure(x => x != Guid.Empty, new Error("The id must not be empty."))
            .FlatMap(async rangeId => await unitOfWork.NormalFiscalSequences.GetByCondictionsAsync(
                range => range.Id == rangeId && range.CompanyId == companyId, ct), ex => new Error(ex.Message))
            .Ensure(range => range is not null, new Error("The range does not exist."))
            .Map(range => range!.MapTo<NormalFiscalSequence, NormalFiscalSequenceDto>())
            .Run();
    }

    public async Task<Either<Error, NormalFiscalSequenceDto>> CreateAsync(
        SaveNormalFiscalSequenceDto request, Guid companyId, CancellationToken ct = default)
    {
        var company = await unitOfWork.Companies.GetCompanyByIdAsync(companyId, ct);
        var issuers = await unitOfWork.CompanyIssuers.IssuerTaxIdsAsync(companyId, ct);
        var existing = await FindAsync(request, companyId, ct);

        return await EitherAsync<Error, SaveNormalFiscalSequenceDto>
            .FromRight(request)
            .Ensure(r => existing is null, new Error("This range is already configured."))
            .Ensure(r => r.From >= 1, new Error("The range cannot start before one."))
            .Ensure(r => r.To <= NormalFiscalSequence.MaxSequence,
                new Error($"The range cannot go beyond {NormalFiscalSequence.MaxSequence}: the number of a normal document carries eight digits."))
            .Ensure(r => r.From <= r.To, new Error("The range cannot end before it starts."))
            .Ensure(r => r.Position >= r.From - 1 && r.Position <= r.To,
                new Error("The position has to be inside the range, or one before its start when no number was used yet."))
            .Ensure(r => !r.RequiresExpiry || r.ExpiresAt is not null,
                new Error("This type of comprobante needs the expiry date of its authorized sequence."))
            .Ensure(r => r.RequiresExpiry || r.ExpiresAt is null,
                new Error("This type of comprobante does not take an expiry date."))
            .Ensure(r => company is not null && issuers.Contains(r.IssuerTaxId),
                new Error("The issuer RNC is not one of the issuers of this company."))
            .Map(r => r.MapTo())
            .Map(range =>
            {
                range.CompanyId = companyId;
                return range;
            })
            .FlatMap(async range =>
            {
                await unitOfWork.NormalFiscalSequences.AddAsync(range, ct);
                await unitOfWork.CommitAsync(ct);
                return range;
            }, ex => new Error(ex.Message))
            .Map(range => range.MapTo<NormalFiscalSequence, NormalFiscalSequenceDto>())
            .Run();
    }

    public async Task<Either<Error, NormalFiscalSequenceDto>> UpdateAsync(
        Guid id, SaveNormalFiscalSequenceDto request, Guid companyId, CancellationToken ct = default)
    {
        var company = await unitOfWork.Companies.GetCompanyByIdAsync(companyId, ct);
        var issuers = await unitOfWork.CompanyIssuers.IssuerTaxIdsAsync(companyId, ct);
        var existing = await unitOfWork.NormalFiscalSequences.GetByIdAsync(id, ct);

        return await EitherAsync<Error, SaveNormalFiscalSequenceDto>
            .FromRight(request)
            .Ensure(r => existing is not null && existing.CompanyId == companyId,
                new Error("The range does not exist."))
            .Ensure(r => existing!.From == r.From && existing.IssuerTaxId == r.IssuerTaxId
                && existing.DocumentType == r.DocumentType,
                new Error("The issuer, the type and the start of a range cannot be changed."))
            .Ensure(r => existing!.CurrentSequence <= r.Position,
                new Error("The position cannot go back: the numbers already used would be handed out again."))
            .Ensure(r => r.From >= 1, new Error("The range cannot start before one."))
            .Ensure(r => r.To <= NormalFiscalSequence.MaxSequence,
                new Error($"The range cannot go beyond {NormalFiscalSequence.MaxSequence}: the number of a normal document carries eight digits."))
            .Ensure(r => r.From <= r.To, new Error("The range cannot end before it starts."))
            .Ensure(r => r.Position >= r.From - 1 && r.Position <= r.To,
                new Error("The position has to be inside the range, or one before its start when no number was used yet."))
            .Ensure(r => !r.RequiresExpiry || r.ExpiresAt is not null,
                new Error("This type of comprobante needs the expiry date of its authorized sequence."))
            .Ensure(r => r.RequiresExpiry || r.ExpiresAt is null,
                new Error("This type of comprobante does not take an expiry date."))
            .Ensure(r => company is not null && issuers.Contains(r.IssuerTaxId),
                new Error("The issuer RNC is not one of the issuers of this company."))
            .Map(r =>
            {
                var range = existing!;
                range.To = r.To;
                range.ExpiresAt = r.ExpiresAt;
                range.CurrentSequence = r.Position;
                range.MarkUpdated();
                return range;
            })
            .FlatMap(async range =>
            {
                await unitOfWork.NormalFiscalSequences.UpdateAsync(range, ct);
                await unitOfWork.CommitAsync(ct);
                return range;
            }, ex => new Error(ex.Message))
            .Map(range => range.MapTo<NormalFiscalSequence, NormalFiscalSequenceDto>())
            .Run();
    }

    private Task<NormalFiscalSequence?> FindAsync(
        SaveNormalFiscalSequenceDto request, Guid companyId, CancellationToken ct)
        => unitOfWork.NormalFiscalSequences.GetByCondictionsAsync(
            range => range.CompanyId == companyId
                && range.IssuerTaxId == request.IssuerTaxId
                && range.DocumentType == request.DocumentType
                && range.From == request.From, ct);
}
