using System.Net;
using System.Security.Cryptography.X509Certificates;
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

    public async Task<Either<Error, ElectronicInvoiceDto>> GetByIdAsync(
        Guid invoiceId, Guid companyId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, Guid>
            .FromRight(invoiceId)
            .Ensure(id => id != Guid.Empty, new Error("invoiceId must not be empty."))
            .Ensure(_ => companyId != Guid.Empty, new Error("companyId must not be empty."))
            .FlatMap(async id => await unitOfWork.ElectronicInvoices.GetByCompanyAndIdAsync(companyId, id, ct),
                ex => new Error(ex.Message))
            .Ensure(invoice => invoice is not null, new Error("The e-CF does not exist.", ErrorKind.NotFound))
            .Map(invoice => invoice!.MapTo<ElectronicInvoice, ElectronicInvoiceDto>())
            .Run();
    }

    public async Task<Either<Error, ElectronicInvoiceDto>> RetryAsync(
        Guid invoiceId, Guid companyId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, Guid>
            .FromRight(invoiceId)
            .Ensure(id => id != Guid.Empty, new Error("invoiceId must not be empty."))
            .Ensure(_ => companyId != Guid.Empty, new Error("companyId must not be empty."))
            .FlatMap(async id => await unitOfWork.ElectronicInvoices.GetByCompanyAndIdAsync(companyId, id, ct),
                ex => new Error(ex.Message))
            .Ensure(invoice => invoice is not null, new Error("The e-CF does not exist.", ErrorKind.NotFound))
            .Ensure(invoice => invoice!.Status == EcfStatus.Draft && invoice.RequiresReview,
                new Error("The e-CF does not wait for a review."))
            .FlatMap(async invoice => await RetryTheDocumentAsync(invoice!, companyId, ct), ex => new Error(ex.Message))
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

        // The state of the taxpayer decides whether a replacement can leave:
        // the contingency it declared is what it is right now, not what it
        // was when the document was created. It is only read when the batch
        // carries a replacement.
        var company = pending.Any(candidate => candidate.ReplacesContingencyDocument)
            ? await unitOfWork.Companies.GetCompanyByIdAsync(companyId, ct)
            : null;

        var leaseOwner = $"{Environment.MachineName}:{Guid.NewGuid():N}";
        var processed = 0;

        foreach (var candidate in pending)
        {
            // The replacement of a paper document waits for the contingency to
            // be lifted: the norm sends it to the DGII from that moment on.
            if (candidate.ReplacesContingencyDocument && TheContingencyIsOpen(company, now))
            {
                await TakeTheWaitingReplacementAsync(companyId, candidate.Id, company, now, ct);
                continue;
            }

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
                if (!invoice.ReplacesContingencyDocument && _window.PassedTheWindow(invoice.CreatedAt, now))
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
                // What the DGII sent is the answer of the last conversation,
                // even when the conversation failed.
                RecordTheBodyOfTheDgii(invoice, exception);

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

    // The taxpayer is still inside the contingency it declared.
    private static bool TheContingencyIsOpen(Company? company, DateTime now)
        => company is not null && CompanyEmissionPolicy.IsInContingency(company, now);

    // The norm does not allow the contingency to last more than fifteen
    // days: a replacement that waits longer than that needs a human.
    private async Task TakeTheWaitingReplacementAsync(
        Guid companyId,
        Guid invoiceId,
        Company? company,
        DateTime now,
        CancellationToken ct)
    {
        if (company?.ContingencyStartsAt is null)
            return;

        if (!_window.PassedTheCeiling(company.ContingencyStartsAt.Value, now))
            return;

        var invoice = await unitOfWork.ElectronicInvoices.GetByCompanyAndIdAsync(companyId, invoiceId, ct);

        if (invoice is null)
            return;

        invoice.MarkForReview(
            "The contingency of the taxpayer is still open after the fifteen days the norm allows.");
        invoice.MarkUpdated();

        await unitOfWork.ElectronicInvoices.UpdateAsync(invoice, ct);
        await unitOfWork.CommitAsync(ct);
    }

    private async Task SendPendingAsync(ElectronicInvoice invoice, DateTime now, CancellationToken ct)
    {
        var certificate = await certificateProvider.GetSigningCertificateAsync(invoice.IssuerTaxId, ct);
        var token = await AuthenticateAsync(invoice.IssuerTaxId, certificate, ct);

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
                invoice.RecordDgiiResponse(receivedAt: lookup.ReceivedAt?.UtcDateTime);
                invoice.MarkSent(
                    lookup.TrackId!,
                    lookup.ReceivedAt?.UtcDateTime ?? now);
                return;
            }
        }

        var receipt = await dgiiClient.SendElectronicInvoiceAsync(invoice.SignedXml, token.Token, ct);

        // The acknowledgement is a conversation with the DGII: its body says
        // what it answered.
        invoice.RecordDgiiResponse(receipt.RawBody);

        if (receipt.IsRejected)
        {
            invoice.Reject(receipt.Error ?? receipt.Message ?? "The DGII refused the e-CF.");
            return;
        }

        if (string.IsNullOrWhiteSpace(receipt.TrackId))
            throw new InvalidOperationException("The DGII answered the e-CF without a track id.");

        invoice.MarkSent(receipt.TrackId, now);
    }

    // The conversation of the token is a credential: its body can carry the
    // token of the DGII, so a failure of it travels without its body and the
    // body never goes on record. The reason of the failure is still there.
    private async Task<DgiiToken> AuthenticateAsync(
        string issuerTaxId, X509Certificate2 certificate, CancellationToken ct)
    {
        try
        {
            return await authenticator.AuthenticateAsync(issuerTaxId, certificate, ct);
        }
        catch (DgiiException exception)
        {
            throw new DgiiException(
                exception.Message,
                innerException: exception,
                statusCode: exception.StatusCode);
        }
    }

    private void ScheduleTheNextAttempt(ElectronicInvoice invoice, Exception exception, DateTime now)
    {
        var attemptsAlreadyMade = Math.Max(0, invoice.AttemptCount - 1);

        // The reason of the failure lives beside the answer of the DGII: one
        // does not wipe the other.
        invoice.LastError = exception.Message;
        invoice.ScheduleNextAttempt(now.Add(_retryBackoff.DelayFor(attemptsAlreadyMade)));
    }

    // The body the DGII sent is the answer of the last conversation with it.
    private static void RecordTheBodyOfTheDgii(ElectronicInvoice invoice, Exception exception)
    {
        if (exception is DgiiException { RawBody: { } body })
            invoice.RecordDgiiResponse(body);
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
                RecordTheBodyOfTheDgii(invoice, exception);
                invoice.LastError = exception.Message;
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
        var token = await AuthenticateAsync(issuerTaxId, certificate, ct);

        // Without a track id the answer of the send was lost. The DGII is asked
        // whether it has the document instead of sending it again.
        if (string.IsNullOrWhiteSpace(invoice.TrackId))
        {
            var lookup = await dgiiClient.GetTrackIdAsync(issuerTaxId, invoice.ENCF, token.Token, ct);

            // The DGII never received it: the document stays pending and the
            // queue is the one that sends it.
            if (lookup.NotFound)
                return false;

            invoice.RecordDgiiResponse(receivedAt: lookup.ReceivedAt?.UtcDateTime);
            invoice.MarkSent(lookup.TrackId, lookup.ReceivedAt?.UtcDateTime ?? DateTime.UtcNow);
        }

        var status = await dgiiClient.GetStatusAsync(invoice.TrackId, token.Token, ct);

        // The answer of the DGII: its body and the two facts it states about
        // the document.
        invoice.RecordDgiiResponse(
            status.RawBody,
            status.SequenceConsumed,
            status.ReceivedAt?.UtcDateTime);

        // A track id the DGII does not know is a failed query, not a verdict.
        if (status.Verdict.ToEcfStatus() is null)
            throw new InvalidOperationException($"The DGII does not know the track id {invoice.TrackId}.");

        ApplyVerdict(invoice, status);

        return true;
    }

    private async Task<ElectronicInvoice> RetryTheDocumentAsync(
        ElectronicInvoice invoice,
        Guid companyId,
        CancellationToken ct)
    {
        var sale = await unitOfWork.Sales.GetSaleByIdAsync(invoice.SaleId, ct);

        if (sale is null)
            throw new InvalidOperationException($"The sale {invoice.SaleId} does not exist.");

        if (sale.CompanyId != companyId)
            throw new InvalidOperationException($"The sale {invoice.SaleId} belongs to another company.");

        var issuer = await unitOfWork.Companies.GetCompanyByIdAsync(companyId, ct);

        if (issuer is null)
            throw new InvalidOperationException($"The company {companyId} does not exist.");

        // The document keeps the issuer it was created with: the retry signs
        // it with the same RNC the document carries.
        if (string.IsNullOrWhiteSpace(invoice.IssuerTaxId))
            throw new InvalidOperationException("The document does not carry the RNC of its issuer.");

        var issuerIdentity = await IssuerIdentityAsync(issuer, invoice.IssuerTaxId, ct);

        var signedAt = DateTime.UtcNow;

        try
        {
            EcfReplacement? replacement = null;

            if (invoice.ReplacesContingencyDocument)
                replacement = await ThePaperDocumentThatTheInvoiceReplacesAsync(sale, companyId, ct);

            var signed = await BuildAndSignTheDocumentAsync(
                invoice,
                sale,
                issuer,
                issuerIdentity,
                replacement,
                companyId,
                signedAt,
                ct);

            invoice.SignAfterReview(signed.SignedXml, signedAt);
            invoice.SecurityCode = signed.SecurityCode;
            invoice.QrUrl = signed.QrUrl;
        }
        catch (Exception exception)
        {
            invoice.MarkForReview(exception.Message);
            invoice.MarkUpdated();

            await unitOfWork.ElectronicInvoices.UpdateAsync(invoice, ct);
            await unitOfWork.CommitAsync(ct);

            throw;
        }

        invoice.MarkUpdated();
        await unitOfWork.ElectronicInvoices.UpdateAsync(invoice, ct);
        await unitOfWork.CommitAsync(ct);

        return invoice;
    }

    private async Task<ElectronicInvoice> IssueTheDocumentAsync(Guid saleId, Guid companyId, CancellationToken ct)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

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

        // The sale is issued by the RNC it names: the RNC of the tenant
        // itself when it does not name one.
        var issuerTaxId = await unitOfWork.CompanyIssuers.ResolveIssuerTaxIdAsync(companyId, sale.IssuerTaxId, ct);
        var issuerIdentity = await IssuerIdentityAsync(issuer, issuerTaxId, ct);

        // The taxpayer in contingency issues on paper and owes the DGII the
        // replacement of every paper document: it is created next to it and
        // it waits for the contingency to be lifted.
        var replacement = await TheReplacementOfThePaperDocumentAsync(sale, issuer, companyId, now, ct);

        var documentType = sale.DocumentType;
        var sequence = await unitOfWork.ElectronicInvoiceSequences.NextSequenceAsync(companyId, issuerTaxId, documentType, ct);
        var encf = ElectronicInvoiceSequence.BuildEncf(documentType, sequence);
        var signedAt = DateTime.UtcNow;

        var invoice = new ElectronicInvoice
        {
            Id = Guid.CreateVersion7(),
            CompanyId = companyId,
            SaleId = saleId,
            ENCF = encf,
            DocumentType = documentType,
            IssuerTaxId = issuerTaxId,
            ReceiverTaxId = sale.ReceptorTaxId,
            ReplacesContingencyDocument = replacement is not null,
            SendNotBefore = replacement is null ? null : issuer.ContingencyEndsAt
        };

        try
        {
            var signed = await BuildAndSignTheDocumentAsync(
                invoice,
                sale,
                issuer,
                issuerIdentity,
                replacement,
                companyId,
                signedAt,
                ct);

            invoice.Sign(signed.SignedXml, signedAt);
            invoice.SecurityCode = signed.SecurityCode;
            invoice.QrUrl = signed.QrUrl;
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

        // A contingency without its exit has no moment to send the
        // replacement from: a human has to take it.
        if (invoice.ReplacesContingencyDocument && invoice.SendNotBefore is null)
        {
            invoice.MarkForReview("The contingency of the taxpayer has no exit date.");
            invoice.MarkUpdated();

            await unitOfWork.ElectronicInvoices.UpdateAsync(invoice, ct);
            await unitOfWork.CommitAsync(ct);
        }

        return invoice;
    }

    // The identity of the issuer: its RNC and the legal name the DGII
    // reads. A branch of the tenant issues with its own name.
    private async Task<IssuerIdentity> IssuerIdentityAsync(Company company, string issuerTaxId, CancellationToken ct)
    {
        if (issuerTaxId == company.TaxId)
            return new IssuerIdentity(issuerTaxId, company.LegalName);

        var issuer = await unitOfWork.CompanyIssuers.GetByCondictionsAsync(
            candidate => candidate.CompanyId == company.Id && candidate.TaxId == issuerTaxId, ct);

        // The branch that issues is on record: without it, the document
        // would carry the RNC of the branch and the name of the company.
        if (issuer is null)
            throw new InvalidOperationException(
                $"The issuer {issuerTaxId} is not registered for this company.");

        return new IssuerIdentity(issuerTaxId, issuer.LegalName);
    }

    private async Task<SignedDocument> BuildAndSignTheDocumentAsync(
        ElectronicInvoice invoice,
        Sale sale,
        Company issuer,
        IssuerIdentity issuerIdentity,
        EcfReplacement? replacement,
        Guid companyId,
        DateTime signedAt,
        CancellationToken ct)
    {
        var sequence = ElectronicInvoiceSequence.SequenceOf(invoice.ENCF);

        // The range the number came from is the one that holds it, and it
        // carries the day the authorized sequence expires.
        var range = await unitOfWork.ElectronicInvoiceSequences.GetByCondictionsAsync(
            r => r.CompanyId == companyId
                && r.IssuerTaxId == issuerIdentity.TaxId
                && r.DocumentType == invoice.DocumentType
                && r.From <= sequence && sequence <= r.To,
            ct);

        // Fail-closed: the types that need the expiry of the authorized
        // sequence fail here until that date is on record.
        var document = SaleEcfDocumentMapper.Map(sale, issuer, issuerIdentity, new EcfIssuance
        {
            ENCF = invoice.ENCF,
            SequenceExpiresAt = range?.ExpiresAt,
            SignedAt = signedAt,
            Replacement = replacement
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
        var certificate = await certificateProvider.GetSigningCertificateAsync(issuerIdentity.TaxId, ct);
        var signedXml = signer.Sign(xml, certificate);

        // The security code the taxpayer prints next to the QR: it comes
        // out of the signature, so it is only known once it is signed.
        var securityCode = EcfSecurityCode.From(signedXml);

        // The URL the receiver opens to check the document. It travels
        // with the document, so it is only known once the amounts and the
        // security code are.
        var qrUrl = VerificationUrlOf(document, invoice.ENCF, securityCode);

        return new SignedDocument(signedXml, securityCode, qrUrl);
    }

    // The paper document the e-CF replaces. It is the document of the sale:
    // its number and its day are the reference the norm asks for.
    private async Task<EcfReplacement?> TheReplacementOfThePaperDocumentAsync(
        Sale sale,
        Company issuer,
        Guid companyId,
        DateTime now,
        CancellationToken ct)
    {
        if (!CompanyEmissionPolicy.IsInContingency(issuer, now))
            return null;

        return await ThePaperDocumentThatTheInvoiceReplacesAsync(sale, companyId, ct);
    }

    private async Task<EcfReplacement> ThePaperDocumentThatTheInvoiceReplacesAsync(
        Sale sale,
        Guid companyId,
        CancellationToken ct)
    {
        // The row already says that this document replaces a paper document:
        // the contingency may be over and the reference still belongs to it.
        var paper = await unitOfWork.NormalFiscalDocuments.GetBySaleIdAsync(companyId, sale.Id, ct);

        if (paper is null)
            throw new InvalidOperationException(
                $"The sale {sale.Id} has no paper document to replace.");

        // The norm: the type of the e-CF is the equivalent type of the paper
        // document it replaces.
        var paperType = NormalFiscalDocumentTypes.For(sale.DocumentType);

        if (paperType is null || paperType.Value != paper.DocumentType)
            throw new InvalidOperationException(
                $"The paper document {paper.Number} is not the counterpart of the e-CF {(int)sale.DocumentType}.");

        return new EcfReplacement(paper.Number, paper.IssuedAt);
    }

    // A consumption invoice below RD$250,000 travels to the DGII as a summary
    // and its printed representation carries the timbre of that summary. The
    // rest of the documents carry the timbre of the host of the taxpayer,
    // which is the URL the QR of their printed representation points at.
    private string? VerificationUrlOf(EcfDocument document, string encf, string securityCode)
    {
        var issuerTaxId = document.Issuer.TaxId;
        var total = document.Totals.Total;

        if (document.DocumentType == EcfDocumentType.Consumer && total < RfceDocument.MaximumAmount)
            return EcfVerificationUrl.ForRfceTimbre(
                _dgiiOptions.Environment, issuerTaxId, encf, total, securityCode);

        return EcfVerificationUrl.ForTimbre(
            _dgiiOptions.Environment,
            issuerTaxId,
            document.Buyer?.TaxId,
            encf,
            total,
            document.Issuer.IssuedAt,
            document.SignedAt,
            securityCode);
    }

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

    private sealed record SignedDocument(string SignedXml, string SecurityCode, string? QrUrl);
}
