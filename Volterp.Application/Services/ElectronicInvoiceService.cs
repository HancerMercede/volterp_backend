using System.Net;
using EitherWay;
using Volterp.Application.DTOs.ElectronicInvoiceDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Interfaces;
using Volterp.Application.Mappers;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.Domain.Policies;
using Volterp.ElectronicInvoicing;
using Volterp.ElectronicInvoicing.Abstractions;
using Volterp.ElectronicInvoicing.Dgii;
using Volterp.ElectronicInvoicing.Qr;
using Volterp.ElectronicInvoicing.Signing;
using Volterp.ElectronicInvoicing.Xml;

namespace Volterp.Application.Services;

public class ElectronicInvoiceService(
    IUnitOfWork unitOfWork,
    EcfXmlWriter writer,
    IEcfSigner signer,
    ISigningCertificateProvider certificateProvider,
    DgiiAuthenticator authenticator,
    IDgiiClient dgiiClient,
    VerdictCheckBackoff? backoff = null,
    DgiiOptions? dgiiOptions = null,
    EmissionRetryBackoff? retryBackoff = null,
    TimeProvider? timeProvider = null) : IElectronicInvoiceService
{
    private const int EmissionBatchSize = 50;
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);

    private readonly VerdictCheckBackoff _backoff = backoff ?? VerdictCheckBackoff.Default();
    private readonly EmissionRetryBackoff _retryBackoff = retryBackoff ?? EmissionRetryBackoff.Default();
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private readonly ContingencyWindow _window = new();

    // The environment the ERP talks to the DGII in: the verification URL of
    // a document points at the same one.
    private readonly DgiiOptions _dgiiOptions = dgiiOptions ?? new DgiiOptions();

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

    public async Task<Either<Error, ElectronicInvoiceDto>> RecoverAsync(
        Guid companyId, string issuerTaxId, string encf, CancellationToken ct = default)
    {
        return await EitherAsync<Error, string>
            .FromRight(encf)
            .Ensure(e => !string.IsNullOrWhiteSpace(e), new Error("The e-NCF cannot be empty."))
            .Ensure(_ => companyId != Guid.Empty, new Error("companyId must not be empty."))
            .Ensure(_ => !string.IsNullOrWhiteSpace(issuerTaxId), new Error("The issuer RNC cannot be empty."))
            .FlatMap(async e => await RecoverTheDocumentAsync(companyId, issuerTaxId, e, ct), ex => new Error(ex.Message))
            .Map(invoice => invoice.MapTo<ElectronicInvoice, ElectronicInvoiceDto>())
            .Run();
    }

    public async Task<Either<Error, int>> RetrackAsync(Guid companyId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, Guid>
            .FromRight(companyId)
            .Ensure(id => id != Guid.Empty, new Error("companyId must not be empty."))
            .FlatMap(async id => await RetrackTheDueDocumentsAsync(id, ct), ex => new Error(ex.Message))
            .Run();
    }

    public async Task<Either<Error, int>> ProcessPendingAsync(Guid companyId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, Guid>
            .FromRight(companyId)
            .Ensure(id => id != Guid.Empty, new Error("companyId must not be empty."))
            .FlatMap(async id => await ProcessPendingInvoicesAsync(id, ct), ex => new Error(ex.Message))
            .Run();
    }

    public async Task<Either<Error, ElectronicInvoiceStateSurfaceDto>> GetStateAsync(
        Guid companyId, int limit, CancellationToken ct = default)
    {
        return await EitherAsync<Error, Guid>
            .FromRight(companyId)
            .Ensure(id => id != Guid.Empty, new Error("companyId must not be empty."))
            .Ensure(_ => limit > 0, new Error("limit must be greater than zero."))
            .FlatMap(async id => await BuildTheStateSurfaceAsync(id, limit, ct), ex => new Error(ex.Message))
            .Run();
    }

    private async Task<ElectronicInvoiceStateSurfaceDto> BuildTheStateSurfaceAsync(
        Guid companyId, int limit, CancellationToken ct)
    {
        var open = await unitOfWork.ElectronicInvoices.GetOpenAsync(companyId, ct);

        // One classification for the two: the count and the list come out of
        // the same set of documents, so they cannot disagree.
        var classified = open
            .Select(invoice => (
                Invoice: invoice,
                State: EcfFiscalStateClassifier.Classify(invoice.Status, invoice.RequiresReview)))
            .Where(x => x.State.HasValue)
            .Select(x => (x.Invoice, State: x.State.GetValueOrDefault()))
            .ToList();

        var grouped = classified.GroupBy(x => x.State).ToDictionary(g => g.Key, g => g.Count());

        var counts = new ElectronicInvoiceStateCounts
        {
            PendingEmission = grouped.GetValueOrDefault(EcfFiscalState.PendingEmission),
            InProcess = grouped.GetValueOrDefault(EcfFiscalState.InProcess),
            Rejected = grouped.GetValueOrDefault(EcfFiscalState.Rejected),
            RequiresReview = grouped.GetValueOrDefault(EcfFiscalState.RequiresReview)
        };

        // The list is the most recent ones: the count covers all of them.
        var invoices = classified
            .OrderByDescending(x => x.Invoice.CreatedAt)
            .Take(limit)
            .Select(x => x.Invoice.MapTo<ElectronicInvoice, ElectronicInvoiceStateDto>() with { FiscalState = x.State })
            .ToList();

        return new ElectronicInvoiceStateSurfaceDto
        {
            Counts = counts,
            Invoices = invoices
        };
    }

    private async Task<int> ProcessPendingInvoicesAsync(Guid companyId, CancellationToken ct)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var pending = await unitOfWork.ElectronicInvoices.GetPendingForEmissionAsync(
            companyId,
            now,
            _window.QueueCutoff(now),
            EmissionBatchSize,
            ct);

        var leaseOwner = $"{Environment.MachineName}:{Guid.NewGuid():N}";
        var processed = 0;

        foreach (var candidate in pending)
        {
            var leaseUntil = now.Add(LeaseDuration);
            var claimed = await unitOfWork.ElectronicInvoices.TryClaimAsync(
                companyId,
                candidate.Id,
                leaseOwner,
                leaseUntil,
                now,
                ct);

            if (!claimed)
                continue;

            var invoice = await unitOfWork.ElectronicInvoices.GetByCompanyAndIdAsync(
                companyId,
                candidate.Id,
                ct);

            if (invoice is null)
                continue;

            try
            {
                if (_window.PassedTheWindow(invoice.CreatedAt, now))
                {
                    invoice.MarkForReview(
                        "The e-CF passed the 72-hour window of the contingency instructivo.");
                }
                else
                {
                    await SendPendingAsync(invoice, now, ct);
                }

                processed++;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                if (IsFiscalRejection(exception, out var reason))
                    invoice.Reject(reason);
                else
                    ScheduleTheNextAttempt(invoice, exception, now);
            }

            invoice.ReleaseLease();
            invoice.MarkUpdated();
            await unitOfWork.ElectronicInvoices.UpdateAsync(invoice, ct);
            await unitOfWork.CommitAsync(ct);
        }

        return processed;
    }

    private async Task SendPendingAsync(ElectronicInvoice invoice, DateTime now, CancellationToken ct)
    {
        var certificate = await certificateProvider.GetSigningCertificateAsync(invoice.IssuerTaxId, ct);
        var token = await authenticator.AuthenticateAsync(invoice.IssuerTaxId, certificate, ct);

        // After the first attempt the answer may have been lost. The DGII is
        // asked by e-NCF before the document is sent again.
        if (invoice.AttemptCount > 1)
        {
            var lookup = await dgiiClient.GetTrackIdAsync(
                invoice.IssuerTaxId,
                invoice.ENCF,
                token.Token,
                ct);

            if (!lookup.NotFound)
            {
                invoice.MarkSent(
                    lookup.TrackId!,
                    lookup.ReceivedAt?.UtcDateTime ?? now);
                return;
            }
        }

        var receipt = await dgiiClient.SendElectronicInvoiceAsync(invoice.SignedXml, token.Token, ct);

        if (receipt.IsRejected)
        {
            invoice.Reject(receipt.Error ?? receipt.Message ?? "The DGII refused the e-CF.");
            return;
        }

        if (string.IsNullOrWhiteSpace(receipt.TrackId))
            throw new InvalidOperationException("The DGII answered the e-CF without a track id.");

        invoice.MarkSent(receipt.TrackId, now);
    }

    private void ScheduleTheNextAttempt(ElectronicInvoice invoice, Exception exception, DateTime now)
    {
        var attemptsAlreadyMade = Math.Max(0, invoice.AttemptCount - 1);

        invoice.VerdictNote = exception.Message;
        invoice.ScheduleNextAttempt(now.Add(_retryBackoff.DelayFor(attemptsAlreadyMade)));
    }

    private static bool IsFiscalRejection(Exception exception, out string reason)
    {
        if (exception is DgiiException
            {
                StatusCode: HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity
            } dgiiException)
        {
            reason = string.IsNullOrWhiteSpace(dgiiException.RawBody)
                ? dgiiException.Message
                : dgiiException.RawBody;
            reason = reason.Length > 1000 ? reason[..1000] : reason;
            return true;
        }

        reason = string.Empty;
        return false;
    }

    private async Task<ElectronicInvoice> RecoverTheDocumentAsync(
        Guid companyId, string issuerTaxId, string encf, CancellationToken ct)
    {
        var invoice = await unitOfWork.ElectronicInvoices.GetByEncfAsync(companyId, issuerTaxId, encf, ct);

        if (invoice is null)
            throw new InvalidOperationException($"There is no e-CF {encf} of the issuer {issuerTaxId} for this company.");

        // A verdict that is already final is not asked again.
        if (invoice.Status is EcfStatus.Accepted or EcfStatus.AcceptedConditionally
            or EcfStatus.Rejected or EcfStatus.Annulled)
            return invoice;

        if (await AskTheVerdictAsync(invoice, issuerTaxId, ct))
        {
            // The document was read without tracking, so the change is persisted
            // on purpose.
            await unitOfWork.ElectronicInvoices.UpdateAsync(invoice, ct);
            await unitOfWork.CommitAsync(ct);
        }

        return invoice;
    }

    private async Task<int> RetrackTheDueDocumentsAsync(Guid companyId, CancellationToken ct)
    {
        // The company travels by hand: the retrack runs without an HTTP
        // context, so the query has to bypass the tenant filter.
        var due = await unitOfWork.ElectronicInvoices.GetDueForCheckAsync(companyId, DateTime.UtcNow, ct);

        foreach (var invoice in due)
        {
            try
            {
                if (await AskTheVerdictAsync(invoice, invoice.IssuerTaxId, ct))
                {
                    // The documents are read without tracking, so the change is
                    // persisted on purpose.
                    await unitOfWork.ElectronicInvoices.UpdateAsync(invoice, ct);
                    await unitOfWork.CommitAsync(ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                // One document cannot stop the pass of the others: its failure
                // is kept and its next check is scheduled.
                invoice.VerdictNote = exception.Message;
                ScheduleTheNextCheck(invoice);
                await unitOfWork.ElectronicInvoices.UpdateAsync(invoice, ct);
                await unitOfWork.CommitAsync(ct);
            }
        }

        return due.Count;
    }

    // It answers whether the document changed: a document the DGII never
    // received has no verdict yet.
    private async Task<bool> AskTheVerdictAsync(ElectronicInvoice invoice, string issuerTaxId, CancellationToken ct)
    {
        var certificate = await certificateProvider.GetSigningCertificateAsync(issuerTaxId, ct);
        var token = await authenticator.AuthenticateAsync(issuerTaxId, certificate, ct);

        // Without a track id the answer of the send was lost. The DGII is asked
        // whether it has the document instead of sending it again.
        if (string.IsNullOrWhiteSpace(invoice.TrackId))
        {
            var lookup = await dgiiClient.GetTrackIdAsync(issuerTaxId, invoice.ENCF, token.Token, ct);

            // The DGII never received it: the document stays pending and the
            // queue is the one that sends it.
            if (lookup.NotFound)
                return false;

            invoice.MarkSent(lookup.TrackId, lookup.ReceivedAt?.UtcDateTime ?? DateTime.UtcNow);
        }

        var status = await dgiiClient.GetStatusAsync(invoice.TrackId, token.Token, ct);

        // A track id the DGII does not know is a failed query, not a verdict.
        if (status.Verdict.ToEcfStatus() is null)
            throw new InvalidOperationException($"The DGII does not know the track id {invoice.TrackId}.");

        ApplyVerdict(invoice, status);

        return true;
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
        {
            if (existing.Status == EcfStatus.Draft && existing.RequiresReview)
                throw new InvalidOperationException("The e-CF of the sale is pending signature and requires review.");

            return existing;
        }

        if (sale.DocumentType is EcfDocumentType.DebitNote or EcfDocumentType.CreditNote
            && !sale.HasDocumentReference)
            throw new InvalidOperationException(
                $"The e-CF {(int)sale.DocumentType} requires the document it modifies.");

        var issuer = await unitOfWork.Companies.GetCompanyByIdAsync(companyId, ct);

        if (issuer is null)
            throw new InvalidOperationException($"The company {companyId} does not exist.");

        var documentType = sale.DocumentType;
        var sequence = await unitOfWork.ElectronicInvoiceSequences.NextSequenceAsync(companyId, documentType, ct);
        var encf = ElectronicInvoiceSequence.BuildEncf(documentType, sequence);
        var signedAt = DateTime.UtcNow;

        var invoice = new ElectronicInvoice
        {
            Id = Guid.CreateVersion7(),
            CompanyId = companyId,
            SaleId = saleId,
            ENCF = encf,
            DocumentType = documentType,
            IssuerTaxId = issuer.TaxId,
            ReceiverTaxId = sale.ReceptorTaxId
        };

        try
        {
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

            var itbis = document.Totals.ItbisTotal ?? 0m;

            // The amounts of the document the DGII received: the lines add up
            // before the ITBIS, and the breakdown by rate lives in the XML.
            invoice.Subtotal = document.Totals.Total - itbis;
            invoice.Itbis = itbis;
            invoice.Total = document.Totals.Total;

            var xml = writer.Write(document);
            invoice.OriginalXml = xml;

            // The certificate belongs to the issuer and there is no fallback,
            // so the signature fails closed without it.
            var certificate = await certificateProvider.GetSigningCertificateAsync(issuer.TaxId, ct);
            var signedXml = signer.Sign(xml, certificate);

            // The security code the taxpayer prints next to the QR: it comes
            // out of the signature, so it is only known once it is signed.
            var securityCode = EcfSecurityCode.From(signedXml);

            // The URL the receiver opens to check the document. It travels
            // with the document, so it is only known once the amounts and the
            // security code are.
            var qrUrl = VerificationUrlOf(documentType, document.Totals.Total, issuer.TaxId, encf, securityCode);

            invoice.Sign(signedXml, signedAt);
            invoice.SecurityCode = securityCode;
            invoice.QrUrl = qrUrl;
        }
        catch (Exception exception)
        {
            // Once the number is taken, the document has to stay on record:
            // a number without a document is a fiscal hole.
            invoice.MarkForReview(exception.Message);

            await unitOfWork.ElectronicInvoices.AddAsync(invoice, CancellationToken.None);
            await unitOfWork.CommitAsync(CancellationToken.None);

            throw;
        }

        // The document is persisted before it is sent: the queue sends it in
        // the background and the signed document and its number are on
        // record.
        await unitOfWork.ElectronicInvoices.AddAsync(invoice, ct);
        await unitOfWork.CommitAsync(ct);

        return invoice;
    }

    // A consumption invoice below RD$250,000 travels to the DGII as a summary
    // and its printed representation carries the timbre of that summary. The
    // rest of the documents have no timbre of a summary to point at.
    private string? VerificationUrlOf(
        EcfDocumentType documentType, decimal total, string issuerTaxId, string encf, string securityCode)
        => documentType == EcfDocumentType.Consumer && total < RfceDocument.MaximumAmount
            ? EcfVerificationUrl.ForRfceTimbre(_dgiiOptions.Environment, issuerTaxId, encf, total, securityCode)
            : null;

    private void ApplyVerdict(ElectronicInvoice invoice, DgiiStatus status)
    {
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
                ScheduleTheNextCheck(invoice);
                break;
        }
    }

    private void ScheduleTheNextCheck(ElectronicInvoice invoice)
    {
        // The wait grows with the number of checks the document already
        // carries: the DGII takes its time and it is not bothered more than
        // necessary.
        var checks = invoice.StatusCheckCount;

        invoice.ScheduleNextCheck(DateTime.UtcNow.Add(_backoff.DelayFor(checks)));
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
