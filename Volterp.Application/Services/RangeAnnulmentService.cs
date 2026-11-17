using System.Security.Cryptography.X509Certificates;
using EitherWay;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.ElectronicInvoicing.Abstractions;
using Volterp.ElectronicInvoicing.Dgii;
using Volterp.ElectronicInvoicing.Signing;
using Volterp.ElectronicInvoicing.Xml;

namespace Volterp.Application.Services;

// The numbers of a range that the taxpayer never used are announced to the
// DGII with the "Anulación de e-NCF" service, in a document signed with the
// certificate of the issuer.
//
// The annulment is asked for: a range that is not annulled keeps its tail,
// and a range that is annulled is closed and never used again.
public class RangeAnnulmentService(
    IUnitOfWork unitOfWork,
    IEcfSigner signer,
    ISigningCertificateProvider certificateProvider,
    DgiiAuthenticator authenticator,
    IDgiiClient dgiiClient) : IRangeAnnulmentService
{
    // The writer of the annulment reads the schema that ships with the
    // library: it holds no state of its own.
    private readonly AnecfXmlWriter _writer = new();

    public async Task<Either<Error, RangeAnnulmentResult>> AnnulAsync(
        Guid sequenceId,
        Guid companyId,
        CancellationToken ct = default)
    {
        return await EitherAsync<Error, Guid>
            .FromRight(sequenceId)
            .Ensure(id => id != Guid.Empty, new Error("The id of the range must not be empty."))
            .Ensure(_ => companyId != Guid.Empty, new Error("The company id must not be empty."))
            .FlatMap(async id => await FindTheRangeAsync(id, companyId, ct), ex => new Error(ex.Message))
            .Ensure(range => range is not null, new Error("The range does not exist.", ErrorKind.NotFound))
            .Ensure(range => !range!.IsAnnulled, new Error("The range was already annulled."))
            .Ensure(range => range!.HasRoom, new Error("The range has no numbers left to annul."))
            .FlatMap(async range => await AnnulTheRangeAsync(range!, ct), ex => new Error(ex.Message))
            .Run();
    }

    // The tenant filter is off without an HTTP context, so the company
    // travels by hand: the range of another company is not ours to annul.
    private async Task<ElectronicInvoiceSequence?> FindTheRangeAsync(
        Guid sequenceId,
        Guid companyId,
        CancellationToken ct)
    {
        var range = await unitOfWork.ElectronicInvoiceSequences.GetByIdAsync(sequenceId, ct);

        return range is not null && range.CompanyId == companyId ? range : null;
    }

    private async Task<RangeAnnulmentResult> AnnulTheRangeAsync(
        ElectronicInvoiceSequence range,
        CancellationToken ct)
    {
        // The certificate belongs to the issuer and there is no fallback, so
        // the signature fails closed without it.
        var certificate = await certificateProvider.GetSigningCertificateAsync(range.IssuerTaxId, ct);
        var token = await AuthenticateAsync(range.IssuerTaxId, certificate, ct);

        var xml = _writer.Write(BuildTheDocument(range));
        var signed = signer.Sign(xml, certificate);

        var answer = await dgiiClient.AnnulRangesAsync(signed, token.Token, ct);

        // The DGII did not annul the ranges: the range stays exactly as it
        // was and the answer travels to whoever asked for the annulment.
        if (!answer.IsAnnulled)
            throw new InvalidOperationException(WhyTheDgiiRefused(answer));

        var annulledAt = DateTime.UtcNow;

        range.MarkAnnulled(annulledAt, answer.RawBody);
        range.MarkUpdated();

        await unitOfWork.ElectronicInvoiceSequences.UpdateAsync(range, ct);
        await unitOfWork.CommitAsync(ct);

        return new RangeAnnulmentResult(answer.Code, answer.Name, answer.Messages, annulledAt);
    }

    // The hole of a range is the tail it never used: the allocator hands out
    // the numbers in order, so the position of the range is the last number
    // that was used.
    private static AnecfDocument BuildTheDocument(ElectronicInvoiceSequence range) => new()
    {
        IssuerTaxId = range.IssuerTaxId,
        GeneratedAt = DateTime.UtcNow,
        Annulments =
        [
            new AnecfAnnulment
            {
                DocumentType = range.DocumentType,
                Sequences =
                [
                    new AnecfSequenceRange
                    {
                        From = ElectronicInvoiceSequence.BuildEncf(range.DocumentType, range.CurrentSequence + 1),
                        To = ElectronicInvoiceSequence.BuildEncf(range.DocumentType, range.To)
                    }
                ]
            }
        ]
    };

    // The conversation of the token is a credential: its body can carry the
    // token of the DGII, so a failure of it travels without its body.
    private async Task<DgiiToken> AuthenticateAsync(
        string issuerTaxId,
        X509Certificate2 certificate,
        CancellationToken ct)
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

    private static string WhyTheDgiiRefused(DgiiRangeAnnulmentAnswer answer)
    {
        var detail = string.Join("; ", answer.Messages.Where(message => !string.IsNullOrWhiteSpace(message)));
        var reason = string.IsNullOrWhiteSpace(answer.Name) ? answer.Code : $"{answer.Code} {answer.Name}";

        return $"The DGII did not annul the ranges ({reason}). {detail}".Trim();
    }
}
