using EitherWay;
using Volterp.Application.DTOs.NormalFiscalDocumentDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.ElectronicInvoicing;

namespace Volterp.Application.Services;

// The paper document of a sale. Its number comes from the range the DGII
// authorized for the normal documents of the issuer, and the number, the
// document and the completion of the sale land in one transaction: a number
// without a document is a fiscal hole.
public class NormalFiscalDocumentService(IUnitOfWork unitOfWork) : INormalFiscalDocumentService
{
    public async Task<Either<Error, NormalFiscalDocumentDto>> IssueAsync(
        Guid saleId, Guid companyId, Guid? userId = null, CancellationToken ct = default)
    {
        return await EitherAsync<Error, Guid>
            .FromRight(saleId)
            .Ensure(id => id != Guid.Empty, new Error("saleId must not be empty."))
            .Ensure(_ => companyId != Guid.Empty, new Error("companyId must not be empty."))
            .FlatMap(async id => await IssueTheDocumentAsync(id, companyId, userId, ct), ex => new Error(ex.Message))
            .Map(document => document.MapTo<NormalFiscalDocument, NormalFiscalDocumentDto>())
            .Run();
    }

    private async Task<NormalFiscalDocument> IssueTheDocumentAsync(
        Guid saleId, Guid companyId, Guid? userId, CancellationToken ct)
    {
        var sale = await unitOfWork.Sales.GetSaleByIdAsync(saleId, ct)
            ?? throw new InvalidOperationException($"The sale {saleId} does not exist.");

        if (sale.CompanyId != companyId)
            throw new InvalidOperationException($"The sale {saleId} belongs to another company.");

        // A sale has at most one normal document: repeating the completion of
        // a sale does not consume another number.
        var existing = await unitOfWork.NormalFiscalDocuments.GetBySaleIdAsync(companyId, sale.Id, ct);

        if (existing is not null)
            return existing;

        var documentType = NormalFiscalDocumentTypes.For(sale.DocumentType)
            ?? throw new InvalidOperationException(
                $"The type {(int)sale.DocumentType} has no normal fiscal document.");

        // A note points at the document it corrects: it never stands alone.
        if (sale.DocumentType is EcfDocumentType.DebitNote or EcfDocumentType.CreditNote
            && !sale.HasDocumentReference)
            throw new InvalidOperationException(
                $"The document {(int)sale.DocumentType} requires the document it modifies.");

        // The paper document is issued by the RNC the sale names: the RNC of
        // the tenant itself when it does not name one.
        var issuerTaxId = await unitOfWork.CompanyIssuers.ResolveIssuerTaxIdAsync(companyId, sale.IssuerTaxId, ct);

        // The number is taken inside the transaction of the unit of work, so
        // the document that carries it and the sale that completes land
        // together.
        await unitOfWork.BeginTransactionAsync(ct);

        try
        {
            // The number is taken from the range of the normal documents, a
            // range of its own: the electronic one is not touched.
            var sequence = await unitOfWork.NormalFiscalSequences
                .NextSequenceAsync(companyId, issuerTaxId, documentType, ct);

            var document = new NormalFiscalDocument
            {
                Id = Guid.CreateVersion7(),
                CompanyId = companyId,
                SaleId = sale.Id,
                Number = NormalFiscalDocument.BuildNumber(documentType, sequence),
                DocumentType = documentType,
                IssuerTaxId = issuerTaxId,
                ReceiverTaxId = sale.ReceptorTaxId,
                ReceiverName = sale.ClienteName,
                Subtotal = sale.Subtotal,
                Itbis = sale.Itbis,
                Total = sale.Total,
                IssuedAt = DateTime.UtcNow
            };

            if (userId.HasValue)
                document.SetCreatedBy(userId.Value);

            await unitOfWork.NormalFiscalDocuments.AddAsync(document, ct);
            await unitOfWork.CommitAsync(ct);

            return document;
        }
        catch
        {
            // The number goes back with the transaction: the sequence does not
            // move when the document does not land.
            await unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }
    }
}
