using EitherWay;
using Volterp.Application.DTOs.ElectronicInvoiceDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Interfaces;
using Volterp.Application.Mappers;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.ElectronicInvoicing;
using Volterp.ElectronicInvoicing.Abstractions;
using Volterp.ElectronicInvoicing.Dgii;
using Volterp.ElectronicInvoicing.Xml;

namespace Volterp.Application.Services;

public class ElectronicInvoiceService(
    IUnitOfWork unitOfWork,
    EcfXmlWriter writer,
    IEcfSigner signer,
    ISigningCertificateProvider certificateProvider,
    DgiiAuthenticator authenticator,
    IDgiiClient dgiiClient) : IElectronicInvoiceService
{
    public async Task<Either<Error, ElectronicInvoiceDto>> IssueAsync(Guid saleId, Guid companyId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, Guid>
            .FromRight(saleId)
            .Ensure(id => id != Guid.Empty, new Error("saleId must not be empty."))
            .Ensure(_ => companyId != Guid.Empty, new Error("companyId must not be empty."))
            .FlatMap(async id => await IssueTheDocumentAsync(id, companyId, ct), ex => new Error(ex.Message))
            .Map(invoice => invoice.MapTo<ElectronicInvoice, ElectronicInvoiceDto>())
            .Run();
    }

    private async Task<ElectronicInvoice> IssueTheDocumentAsync(Guid saleId, Guid companyId, CancellationToken ct)
    {
        // Without an HTTP context the global tenant filter is off, so the sale
        // of another company is readable: the company travels by hand.
        var sale = await unitOfWork.Sales.GetSaleByIdAsync(saleId, ct);

        if (sale is null)
            throw new InvalidOperationException($"The sale {saleId} does not exist.");

        if (sale.CompanyId != companyId)
            throw new InvalidOperationException($"The sale {saleId} belongs to another company.");

        if (sale.Status != SaleStatus.Completed)
            throw new InvalidOperationException($"The sale {saleId} is not completed.");

        // The number is taken once: a sale that already has a document is not
        // issued again.
        var existing = await unitOfWork.ElectronicInvoices.GetBySaleIdAsync(saleId, ct);

        if (existing is not null)
            return existing;

        if (sale.DocumentType is EcfDocumentType.DebitNote or EcfDocumentType.CreditNote
            && !sale.HasDocumentReference)
            throw new InvalidOperationException(
                $"The e-CF {(int)sale.DocumentType} requires the document it modifies.");

        var issuer = await unitOfWork.Companies.GetCompanyByIdAsync(companyId, ct);

        if (issuer is null)
            throw new InvalidOperationException($"The company {companyId} does not exist.");

        // The certificate belongs to the issuer and there is no fallback, so the
        // issuance fails closed without one. It is resolved before the number
        // is taken, so a missing certificate does not burn an e-NCF.
        var certificate = await certificateProvider.GetSigningCertificateAsync(issuer.TaxId, ct);

        var documentType = sale.DocumentType;
        var sequence = await unitOfWork.ElectronicInvoiceSequences.NextSequenceAsync(companyId, documentType, ct);
        var encf = ElectronicInvoiceSequence.BuildEncf(documentType, sequence);
        var signedAt = DateTime.UtcNow;

        // The range the number came from is the one that holds it, and it
        // carries the day the authorized sequence expires.
        var range = await unitOfWork.ElectronicInvoiceSequences.GetByCondictionsAsync(
            r => r.CompanyId == companyId
                && r.IssuerTaxId == issuer.TaxId
                && r.DocumentType == documentType
                && r.From <= sequence && sequence <= r.To,
            ct);

        // Fail-closed: the types that need the expiry of the authorized
        // sequence fail here until that date is on record.
        var document = SaleEcfDocumentMapper.Map(sale, issuer, new EcfIssuance
        {
            ENCF = encf,
            SequenceExpiresAt = range?.ExpiresAt,
            SignedAt = signedAt
        });

        var xml = writer.Write(document);
        var signedXml = signer.Sign(xml, certificate);

        var itbis = document.Totals.ItbisTotal ?? 0m;

        var invoice = new ElectronicInvoice
        {
            Id = Guid.CreateVersion7(),
            CompanyId = companyId,
            SaleId = saleId,
            ENCF = encf,
            DocumentType = documentType,
            IssuerTaxId = issuer.TaxId,
            ReceiverTaxId = sale.ReceptorTaxId,

            // The amounts of the document the DGII received: the lines add up
            // before the ITBIS, and the breakdown by rate lives in the XML.
            Subtotal = document.Totals.Total - itbis,
            Itbis = itbis,
            Total = document.Totals.Total
        };

        invoice.Sign(signedXml, signedAt);

        // The document is persisted before it is sent: if the answer never
        // arrives, the signed document and its number are on record.
        await unitOfWork.ElectronicInvoices.AddAsync(invoice, ct);
        await unitOfWork.CommitAsync(ct);

        try
        {
            var token = await authenticator.AuthenticateAsync(issuer.TaxId, certificate, ct);
            var receipt = await dgiiClient.SendElectronicInvoiceAsync(signedXml, token.Token, ct);

            if (receipt.IsRejected || string.IsNullOrWhiteSpace(receipt.TrackId))
                throw new InvalidOperationException(
                    $"The DGII refused the e-CF {encf}: {receipt.Error ?? receipt.Message ?? "it answered without a track id."}");

            invoice.MarkSent(receipt.TrackId, DateTime.UtcNow);
            await unitOfWork.CommitAsync(ct);

            var status = await dgiiClient.GetStatusAsync(receipt.TrackId, token.Token, ct);

            // A track id the DGII does not know is a failed query, not a verdict.
            if (status.Verdict.ToEcfStatus() is null)
                throw new InvalidOperationException($"The DGII does not know the track id {receipt.TrackId}.");

            switch (status.Verdict)
            {
                case DgiiVerdict.Accepted:
                    invoice.Accept();
                    break;

                case DgiiVerdict.AcceptedConditionally:
                    invoice.AcceptConditionally(Describe(status, "The DGII accepted the e-CF with an observation it did not detail."));
                    break;

                case DgiiVerdict.Rejected:
                    invoice.Reject(Describe(status, "The DGII rejected the e-CF without explaining why."));
                    break;

                case DgiiVerdict.InProcess:
                    // The DGII is still validating the document: it stays in that
                    // state and the verdict is asked again later.
                    invoice.MarkInProcess();
                    break;
            }

            await unitOfWork.CommitAsync(ct);

            return invoice;
        }
        catch (Exception exception)
        {
            // The document keeps the state it really reached and the reason it
            // stopped there: a document that was never sent never claims to be
            // sent.
            invoice.VerdictNote = exception.Message;
            invoice.MarkUpdated();
            await unitOfWork.CommitAsync(ct);

            throw;
        }
    }

    private static string Describe(DgiiStatus status, string fallback)
    {
        var messages = status.Messages
            .Select(message => message.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();

        if (messages.Count > 0)
            return string.Join("; ", messages);

        return string.IsNullOrWhiteSpace(status.StatusText) ? fallback : status.StatusText;
    }
}
