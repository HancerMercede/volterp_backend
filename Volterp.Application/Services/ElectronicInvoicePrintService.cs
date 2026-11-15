using EitherWay;
using Volterp.Application.DTOs.Printing;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Interfaces;
using Volterp.ElectronicInvoicing;
using Volterp.ElectronicInvoicing.Printing;
using Volterp.ElectronicInvoicing.Signing;

namespace Volterp.Application.Services;

// The printed representation of an e-CF: the paper the receiver keeps when
// it is not electronic.
//
// It is built from the document that was signed: the amounts, the names and
// the address are the ones the DGII received, not the ones the ERP holds
// today.
public class ElectronicInvoicePrintService(IUnitOfWork unitOfWork)
{
    public async Task<Either<Error, ElectronicInvoicePrintDto>> PrintAsync(
        Guid invoiceId,
        Guid companyId,
        CancellationToken ct = default)
    {
        return await EitherAsync<Error, Guid>
            .FromRight(invoiceId)
            .Ensure(id => id != Guid.Empty, new Error("The id of the e-CF cannot be empty."))
            .Ensure(_ => companyId != Guid.Empty, new Error("The company cannot be empty."))
            .FlatMap(async id => await PrintTheDocumentAsync(id, companyId, ct), ex => new Error(ex.Message))
            .Run();
    }

    private async Task<ElectronicInvoicePrintDto> PrintTheDocumentAsync(
        Guid invoiceId,
        Guid companyId,
        CancellationToken ct)
    {
        var invoice = await unitOfWork.ElectronicInvoices.GetByIdAsync(invoiceId, ct);

        // The company travels by hand: the filter of the tenant isolates the
        // document, and the check makes the isolation explicit at the boundary.
        if (invoice is null || invoice.CompanyId != companyId)
            throw new InvalidOperationException("The e-CF does not exist.");

        // The security code comes out of the signature: a document that was
        // not signed has no printed representation.
        if (invoice.Status == EcfStatus.Draft || string.IsNullOrWhiteSpace(invoice.SignedXml))
            throw new InvalidOperationException("The e-CF is not signed.");

        // A document the DGII rejected or annulled has no fiscal credit
        // behind it, so its printed representation cannot support one
        // ("Informe Técnico e-CF v1.0", section 18).
        if (invoice.Status == EcfStatus.Rejected)
            throw new InvalidOperationException(
                "The e-CF was rejected by the DGII: its printed representation cannot support fiscal credit.");

        if (invoice.Status == EcfStatus.Annulled)
            throw new InvalidOperationException(
                "The e-CF was annulled: its printed representation cannot support fiscal credit.");

        // The representation is delivered after the DGII accepts the
        // document. A document that is only signed, sent or still in process
        // is not printable yet.
        if (invoice.Status is not (EcfStatus.Accepted or EcfStatus.AcceptedConditionally)
            || invoice.RequiresReview)
            throw new InvalidOperationException("The e-CF is not in a printable state.");

        if (string.IsNullOrWhiteSpace(invoice.QrUrl))
            throw new InvalidOperationException("The e-CF has no verification URL.");

        var signed = EcfSignedDocumentReader.Read(invoice.SignedXml);
        var securityCode = EcfSecurityCode.From(invoice.SignedXml);

        return new ElectronicInvoicePrintDto
        {
            ElectronicInvoiceId = invoice.Id,
            ENCF = invoice.ENCF,
            DocumentType = invoice.DocumentType,
            SecurityCode = securityCode,
            QrUrl = invoice.QrUrl,
            Document = signed.Document,
            AdditionalTaxes = signed.AdditionalTaxes,
            Pagination = signed.Pagination,
            ItemAmountsIncludeItbis = signed.ItemAmountsIncludeItbis
        };
    }
}
