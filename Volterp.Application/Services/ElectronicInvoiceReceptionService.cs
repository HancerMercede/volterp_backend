using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.Xml;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using EitherWay;
using Volterp.Application.DTOs.ReceivedElectronicInvoiceDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Domain.ValueObjects;
using Volterp.ElectronicInvoicing;

namespace Volterp.Application.Services;

public class ElectronicInvoiceReceptionService(IUnitOfWork unitOfWork) : IElectronicInvoiceReceptionService
{
    public async Task<Either<Error, ReceivedElectronicInvoiceDto>> ReceiveAsync(
        string xml,
        string fileName,
        CancellationToken ct = default)
    {
        return await EitherAsync<Error, string>
            .FromRight(xml)
            .Ensure(value => !string.IsNullOrWhiteSpace(value), new Error("The XML document is required."))
            .FlatMap(
                async _ => await ReceiveTheDocumentAsync(xml, fileName, ct),
                exception => new Error(exception.Message))
            .Map(received => ToDto(received))
            .Run();
    }

    public async Task<Either<Error, ReceivedElectronicInvoiceDto>> ReceiveCommercialApprovalAsync(
        string xml,
        string fileName,
        CancellationToken ct = default)
    {
        return await EitherAsync<Error, string>
            .FromRight(xml)
            .Ensure(value => !string.IsNullOrWhiteSpace(value), new Error("The XML document is required."))
            .FlatMap(
                async _ => await ReceiveTheCommercialApprovalAsync(xml, fileName, ct),
                exception => new Error(exception.Message))
            .Map(received => ToDto(received))
            .Run();
    }

    public async Task<Either<Error, ReceivedElectronicInvoiceDto?>> DecideCommercialApprovalAsync(
        Guid id,
        Guid companyId,
        Guid userId,
        bool approved,
        string reason,
        CancellationToken ct = default)
    {
        return await EitherAsync<Error, Guid>
            .FromRight(id)
            .Ensure(value => value != Guid.Empty, new Error("The id must not be empty."))
            .Ensure(_ => companyId != Guid.Empty, new Error("The company id must not be empty."))
            .Ensure(_ => userId != Guid.Empty, new Error("The user id must not be empty."))
            .Ensure(_ => !string.IsNullOrWhiteSpace(reason), new Error("The reason is required."))
            .FlatMap(
                async invoiceId => await DecideTheCommercialApprovalAsync(
                    invoiceId,
                    companyId,
                    userId,
                    approved,
                    reason,
                    ct),
                exception => new Error(exception.Message))
            .Map(received => received is null ? null : ToDto(received))
            .Run();
    }

    private async Task<ReceivedDocument> ReceiveTheDocumentAsync(
        string xml,
        string fileName,
        CancellationToken ct)
    {
        var document = ParseReceivedEcf(xml);
        EnsureFileNameCorresponds(fileName, document.ReceiverTaxId, document.ENCF);

        var company = await ResolveCompanyAsync(document.ReceiverTaxId, ct);
        var existing = await unitOfWork.ReceivedElectronicInvoices.GetByIssuerAndEncfAsync(
            document.IssuerTaxId,
            document.ENCF,
            ct);

        if (existing is not null)
        {
            if (existing.CompanyId != company.Id)
                throw new InvalidOperationException("The e-CF was already received.");

            return new ReceivedDocument(existing, AlreadyReceived: true);
        }

        var invoice = new ReceivedElectronicInvoice
        {
            Id = Guid.CreateVersion7(),
            CompanyId = company.Id,
            IssuerTaxId = document.IssuerTaxId,
            ReceiverTaxId = document.ReceiverTaxId,
            ENCF = document.ENCF,
            DocumentType = document.DocumentType,
            SignedXml = xml,
            SignedAt = document.SignedAt,
            ReceivedAt = DateTime.UtcNow,
            ApprovalStatus = ReceivedElectronicInvoiceApprovalStatus.Received
        };

        await unitOfWork.ReceivedElectronicInvoices.AddAsync(invoice, ct);
        await unitOfWork.CommitAsync(ct);

        return new ReceivedDocument(invoice, AlreadyReceived: false);
    }

    private async Task<ReceivedDocument> ReceiveTheCommercialApprovalAsync(
        string xml,
        string fileName,
        CancellationToken ct)
    {
        var approval = ParseCommercialApproval(xml);
        EnsureFileNameCorresponds(fileName, approval.ReceiverTaxId, approval.ENCF);

        var company = await ResolveCompanyAsync(approval.ReceiverTaxId, ct);
        var invoice = await unitOfWork.ReceivedElectronicInvoices.GetByIssuerAndEncfAsync(
            approval.IssuerTaxId,
            approval.ENCF,
            ct);

        if (invoice is null
            || invoice.CompanyId != company.Id
            || invoice.ReceiverTaxId != approval.ReceiverTaxId)
        {
            throw new InvalidOperationException(
                "The commercial approval does not correspond to a received e-CF of this tenant.");
        }

        var changed = ApplyDecision(
            invoice,
            approval.Approved,
            approval.ReceiverTaxId,
            approval.DecidedAt,
            approval.Reason);

        if (changed)
        {
            invoice.MarkUpdated();
            await unitOfWork.ReceivedElectronicInvoices.UpdateAsync(invoice, ct);
            await unitOfWork.CommitAsync(ct);
        }

        return new ReceivedDocument(invoice, AlreadyReceived: !changed);
    }

    private async Task<ReceivedDocument?> DecideTheCommercialApprovalAsync(
        Guid id,
        Guid companyId,
        Guid userId,
        bool approved,
        string reason,
        CancellationToken ct)
    {
        var invoice = await unitOfWork.ReceivedElectronicInvoices.GetByCompanyAndIdAsync(
            companyId,
            id,
            ct);

        // The document that does not exist is the absence of the answer, not a
        // failure: the caller tells it apart without reading a message.
        if (invoice is null)
            return null;

        var changed = ApplyDecision(
            invoice,
            approved,
            userId.ToString(),
            DateTime.UtcNow,
            reason);

        if (changed)
        {
            invoice.SetUpdatedBy(userId);
            invoice.MarkUpdated();
            await unitOfWork.ReceivedElectronicInvoices.UpdateAsync(invoice, ct);
            await unitOfWork.CommitAsync(ct);
        }

        return new ReceivedDocument(invoice, AlreadyReceived: !changed);
    }

    private async Task<Company> ResolveCompanyAsync(string receiverTaxId, CancellationToken ct)
    {
        // The receiver RNC identifies the tenant that receives: its own RNC
        // or the one of one of its branches.
        var company = await unitOfWork.CompanyIssuers.CompanyOfIssuerAsync(receiverTaxId, ct);

        if (company is null)
            throw new InvalidOperationException("The receiver RNC is unknown.");

        return company;
    }

    private static bool ApplyDecision(
        ReceivedElectronicInvoice invoice,
        bool approved,
        string decidedBy,
        DateTime decidedAt,
        string? reason)
    {
        var target = approved
            ? ReceivedElectronicInvoiceApprovalStatus.Approved
            : ReceivedElectronicInvoiceApprovalStatus.Rejected;

        if (invoice.ApprovalStatus != ReceivedElectronicInvoiceApprovalStatus.Received)
        {
            if (invoice.ApprovalStatus == target)
                return false;

            throw new InvalidOperationException("The commercial approval was already recorded.");
        }

        if (approved)
            invoice.ApproveCommercial(decidedBy, decidedAt, reason);
        else
            invoice.RejectCommercial(decidedBy, decidedAt, reason ?? string.Empty);

        return true;
    }

    private static ReceivedEcf ParseReceivedEcf(string xml)
    {
        var document = LoadSecureXml(xml);
        var root = document.Root;

        if (root is null || root.Name.LocalName != "ECF")
            throw new InvalidOperationException("The XML document is not an e-CF.");

        EnsureValidSignature(xml);

        var encf = RequiredValue(document, "Encabezado/IdDoc/eNCF");
        var typeValue = RequiredValue(document, "Encabezado/IdDoc/TipoeCF");
        var issuerTaxId = NormalizeRnc(RequiredValue(document, "Encabezado/Emisor/RNCEmisor"), "issuer");
        var receiverTaxId = NormalizeRnc(RequiredValue(document, "Encabezado/Comprador/RNCComprador"), "receiver");

        if (!Regex.IsMatch(encf, "^E[0-9]{12}$", RegexOptions.CultureInvariant))
            throw new InvalidOperationException("The e-NCF is not valid.");

        if (!int.TryParse(typeValue, NumberStyles.None, CultureInfo.InvariantCulture, out var type)
            || !Enum.IsDefined((EcfDocumentType)type))
        {
            throw new InvalidOperationException("The e-CF type is not valid.");
        }

        if (!encf.AsSpan(1, 2).SequenceEqual(((int)type).ToString("00", CultureInfo.InvariantCulture)))
            throw new InvalidOperationException("The e-NCF does not correspond to the e-CF type.");

        var signedAt = RequiredValue(document, "FechaHoraFirma");
        if (!DateTime.TryParseExact(
                signedAt,
                "dd-MM-yyyy HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var moment))
        {
            throw new InvalidOperationException("The signing date is not valid.");
        }

        return new ReceivedEcf(
            issuerTaxId,
            receiverTaxId,
            encf,
            (EcfDocumentType)type,
            DateTime.SpecifyKind(moment, DateTimeKind.Utc));
    }

    private static CommercialApproval ParseCommercialApproval(string xml)
    {
        var document = LoadSecureXml(xml);
        var root = document.Root;

        if (root is null || root.Name.LocalName != "ACECF")
            throw new InvalidOperationException("The XML document is not a commercial approval.");

        EnsureValidSignature(xml);

        var issuerTaxId = NormalizeRnc(
            RequiredValue(document, "DetalleAprobacionComercial/RNCEmisor"),
            "issuer");
        var receiverTaxId = NormalizeRnc(
            RequiredValue(document, "DetalleAprobacionComercial/RNCComprador"),
            "receiver");
        var encf = RequiredValue(document, "DetalleAprobacionComercial/eNCF");

        if (!Regex.IsMatch(encf, "^E[0-9]{12}$", RegexOptions.CultureInvariant))
            throw new InvalidOperationException("The e-NCF is not valid.");

        var state = RequiredValue(document, "DetalleAprobacionComercial/Estado");
        if (state is not ("1" or "2"))
            throw new InvalidOperationException("The commercial approval state is not valid.");

        var decidedAtValue = RequiredValue(
            document,
            "DetalleAprobacionComercial/FechaHoraAprobacionComercial");

        if (!DateTime.TryParseExact(
                decidedAtValue,
                "dd-MM-yyyy HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var decidedAt))
        {
            throw new InvalidOperationException("The commercial approval date is not valid.");
        }

        var reason = Value(document, "DetalleAprobacionComercial/DetalleMotivoRechazo");
        if (state == "2" && string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("The rejection reason is required.");

        return new CommercialApproval(
            issuerTaxId,
            receiverTaxId,
            encf,
            state == "1",
            reason,
            DateTime.SpecifyKind(decidedAt, DateTimeKind.Utc));
    }

    private static XDocument LoadSecureXml(string xml)
    {
        try
        {
            using var reader = XmlReader.Create(
                new StringReader(xml),
                new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    MaxCharactersFromEntities = 0
                });

            return XDocument.Load(reader, LoadOptions.None);
        }
        catch (XmlException exception)
        {
            throw new InvalidOperationException("The XML document is malformed.", exception);
        }
    }

    private static void EnsureValidSignature(string xml)
    {
        try
        {
            using var reader = XmlReader.Create(
                new StringReader(xml),
                new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    MaxCharactersFromEntities = 0
                });

            var document = new XmlDocument { XmlResolver = null, PreserveWhitespace = false };
            document.Load(reader);

            var signatures = document.GetElementsByTagName(
                "Signature",
                SignedXml.XmlDsigNamespaceUrl);

            if (signatures.Count != 1)
                throw new InvalidOperationException("The XML document is not signed.");

            var signature = (XmlElement)signatures[0]!;
            var verifier = new SignedXml(document);
            verifier.LoadXml(signature);

            if (!verifier.CheckSignature())
                throw new InvalidOperationException("The XML signature is not valid.");
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (CryptographicException exception)
        {
            throw new InvalidOperationException("The XML signature is not valid.", exception);
        }
    }

    private static void EnsureFileNameCorresponds(string fileName, string receiverTaxId, string encf)
    {
        var name = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(name)
            || !name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The file name does not follow the reception contract.");
        }

        var stem = name[..^4];
        if (stem.Length < 22)
            throw new InvalidOperationException("The file name does not follow the reception contract.");

        var fileEncf = stem[^13..];
        var fileReceiverTaxId = stem[..^13];

        if (!string.Equals(fileEncf, encf, StringComparison.Ordinal)
            || !string.Equals(fileReceiverTaxId, receiverTaxId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The file name does not correspond to the document.");
        }
    }

    private static string NormalizeRnc(string value, string role)
    {
        if (!Rnc.TryParse(value, out var rnc))
            throw new InvalidOperationException($"The {role} RNC is not valid.");

        return rnc.Value;
    }

    private static string RequiredValue(XDocument document, string path)
        => Value(document, path)
            ?? throw new InvalidOperationException($"The XML document does not carry {path}.");

    private static string? Value(XDocument document, string path)
    {
        XElement? current = document.Root;
        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            current = current?.Elements().FirstOrDefault(element => element.Name.LocalName == segment);
            if (current is null)
                return null;
        }

        var element = current;
        if (element is null)
            return null;

        var value = element.Value.Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    // The entity is mapped into the DTO and the flag of the operation is set
    // on top of it: the flag does not come from the entity.
    private static ReceivedElectronicInvoiceDto ToDto(ReceivedDocument received)
        => received.Invoice.MapTo<ReceivedElectronicInvoice, ReceivedElectronicInvoiceDto>()
            with { AlreadyReceived = received.AlreadyReceived };

    private sealed record ReceivedDocument(
        ReceivedElectronicInvoice Invoice,
        bool AlreadyReceived);

    private sealed record ReceivedEcf(
        string IssuerTaxId,
        string ReceiverTaxId,
        string ENCF,
        EcfDocumentType DocumentType,
        DateTime SignedAt);

    private sealed record CommercialApproval(
        string IssuerTaxId,
        string ReceiverTaxId,
        string ENCF,
        bool Approved,
        string? Reason,
        DateTime DecidedAt);
}
