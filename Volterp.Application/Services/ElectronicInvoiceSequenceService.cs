using EitherWay;
using Volterp.Application.DTOs.ElectronicInvoiceSequenceDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;

namespace Volterp.Application.Services;

// El contribuyente carga el rango que le autorizo la DGII. El rango es el
// mismo para el comprobante electronico y para el normal, asi que la posicion
// que declara es la del ultimo numero que uso, paper incluido.
public class ElectronicInvoiceSequenceService(IUnitOfWork unitOfWork) : IElectronicInvoiceSequenceService
{
    public async Task<PagedResult<ElectronicInvoiceSequenceDto>> GetAllAsync(
        Guid companyId, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var ranges = await unitOfWork.ElectronicInvoiceSequences.GetAllAsync(
            range => range.CompanyId == companyId, pageNumber, pageSize, ct);

        return ranges.MapTo<ElectronicInvoiceSequence, ElectronicInvoiceSequenceDto>();
    }

    public async Task<Either<Error, ElectronicInvoiceSequenceDto>> GetByIdAsync(
        Guid id, Guid companyId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, Guid>
            .FromRight(id)
            .Ensure(x => x != Guid.Empty, new Error("The id must not be empty."))
            .FlatMap(async rangeId => await unitOfWork.ElectronicInvoiceSequences.GetByCondictionsAsync(
                range => range.Id == rangeId && range.CompanyId == companyId, ct), ex => new Error(ex.Message))
            .Ensure(range => range is not null, new Error("The range does not exist."))
            .Map(range => range!.MapTo<ElectronicInvoiceSequence, ElectronicInvoiceSequenceDto>())
            .Run();
    }

    public async Task<Either<Error, ElectronicInvoiceSequenceDto>> CreateAsync(
        SaveElectronicInvoiceSequenceDto request, Guid companyId, CancellationToken ct = default)
    {
        var issuer = await unitOfWork.Companies.GetCompanyByIdAsync(companyId, ct);
        var existing = await FindAsync(request, companyId, ct);

        return await EitherAsync<Error, SaveElectronicInvoiceSequenceDto>
            .FromRight(request)
            .Ensure(r => existing is null, new Error("This range is already configured."))
            .Ensure(r => r.From <= r.To, new Error("The range cannot end before it starts."))
            .Ensure(r => r.Position >= r.From - 1 && r.Position <= r.To,
                new Error("The position has to be inside the range, or one before its start when no number was used yet."))
            .Ensure(r => !r.RequiresExpiry || r.ExpiresAt is not null,
                new Error("This type of comprobante needs the expiry date of its authorized sequence."))
            .Ensure(r => r.RequiresExpiry || r.ExpiresAt is null,
                new Error("This type of comprobante does not take an expiry date."))
            .Ensure(r => issuer is not null && r.IssuerTaxId == issuer.TaxId,
                new Error("The issuer RNC is not the one of this company."))
            .Map(r => r.MapTo())
            .Map(range =>
            {
                range.CompanyId = companyId;
                return range;
            })
            .FlatMap(async range =>
            {
                await unitOfWork.ElectronicInvoiceSequences.AddAsync(range, ct);
                await unitOfWork.CommitAsync(ct);
                return range;
            }, ex => new Error(ex.Message))
            .Map(range => range.MapTo<ElectronicInvoiceSequence, ElectronicInvoiceSequenceDto>())
            .Run();
    }

    public async Task<Either<Error, ElectronicInvoiceSequenceDto>> UpdateAsync(
        Guid id, SaveElectronicInvoiceSequenceDto request, Guid companyId, CancellationToken ct = default)
    {
        var issuer = await unitOfWork.Companies.GetCompanyByIdAsync(companyId, ct);
        var existing = await unitOfWork.ElectronicInvoiceSequences.GetByIdAsync(id, ct);

        return await EitherAsync<Error, SaveElectronicInvoiceSequenceDto>
            .FromRight(request)
            .Ensure(r => existing is not null && existing.CompanyId == companyId,
                new Error("The range does not exist."))
            .Ensure(r => existing!.From == r.From && existing.IssuerTaxId == r.IssuerTaxId
                && existing.DocumentType == r.DocumentType,
                new Error("The issuer, the type and the start of a range cannot be changed."))
            .Ensure(r => existing!.CurrentSequence <= r.Position,
                new Error("The position cannot go back: the numbers already used would be handed out again."))
            .Ensure(r => r.From <= r.To, new Error("The range cannot end before it starts."))
            .Ensure(r => r.Position >= r.From - 1 && r.Position <= r.To,
                new Error("The position has to be inside the range, or one before its start when no number was used yet."))
            .Ensure(r => !r.RequiresExpiry || r.ExpiresAt is not null,
                new Error("This type of comprobante needs the expiry date of its authorized sequence."))
            .Ensure(r => r.RequiresExpiry || r.ExpiresAt is null,
                new Error("This type of comprobante does not take an expiry date."))
            .Ensure(r => issuer is not null && r.IssuerTaxId == issuer.TaxId,
                new Error("The issuer RNC is not the one of this company."))
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
                await unitOfWork.ElectronicInvoiceSequences.UpdateAsync(range, ct);
                await unitOfWork.CommitAsync(ct);
                return range;
            }, ex => new Error(ex.Message))
            .Map(range => range.MapTo<ElectronicInvoiceSequence, ElectronicInvoiceSequenceDto>())
            .Run();
    }

    private Task<ElectronicInvoiceSequence?> FindAsync(
        SaveElectronicInvoiceSequenceDto request, Guid companyId, CancellationToken ct)
        => unitOfWork.ElectronicInvoiceSequences.GetByCondictionsAsync(
            range => range.CompanyId == companyId
                && range.IssuerTaxId == request.IssuerTaxId
                && range.DocumentType == request.DocumentType
                && range.From == request.From, ct);
}
