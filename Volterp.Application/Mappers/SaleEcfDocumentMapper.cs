using Volterp.Domain.Entities;
using Volterp.ElectronicInvoicing;
using Volterp.ElectronicInvoicing.Xml;

namespace Volterp.Application.Mappers;

/// <summary>
/// What the ERP has to provide to issue a document: the number the DGII
/// assigned and the moment the document is signed.
/// </summary>
public sealed record EcfIssuance
{
    /// <summary>The e-NCF of the document.</summary>
    public required string ENCF { get; init; }

    /// <summary>
    /// When the authorized sequence of e-NCF expires. Only the types that
    /// require it use it.
    /// </summary>
    public DateTime? SequenceExpiresAt { get; init; }

    /// <summary>When the document is signed, in the issuer's local time.</summary>
    public required DateTime SignedAt { get; init; }
}

/// <summary>
/// Turns a sale into the document the DGII expects. The rules of each type of
/// document live in its profile, so the same sale becomes a fiscal credit or a
/// consumer invoice without the caller knowing the differences.
/// </summary>
public static class SaleEcfDocumentMapper
{
    /// <summary>
    /// Maps a sale into the document the DGII expects.
    /// </summary>
    /// <param name="sale">The sale to issue.</param>
    /// <param name="issuer">The company that issues the document.</param>
    /// <param name="issuance">The number and the moment of the document.</param>
    /// <exception cref="InvalidOperationException">The sale cannot become a document of its type.</exception>
    public static EcfDocument Map(Sale sale, Company issuer, EcfIssuance issuance)
    {
        ArgumentNullException.ThrowIfNull(sale);
        ArgumentNullException.ThrowIfNull(issuer);
        ArgumentNullException.ThrowIfNull(issuance);

        if (sale.Items.Count == 0)
            throw new InvalidOperationException("The sale has no lines to issue.");

        var profile = EcfTypeProfiles.For(sale.DocumentType);

        if (profile.RequiresSequenceExpiry && issuance.SequenceExpiresAt is null)
            throw new InvalidOperationException(
                $"The e-CF {profile.DocumentType} requires the date the authorized sequence expires.");

        var items = MapItems(sale, profile);

        return new EcfDocument
        {
            DocumentType = sale.DocumentType,
            Identity = new EcfIdentity
            {
                ENCF = issuance.ENCF,
                SequenceExpiresAt = issuance.SequenceExpiresAt,
                TypeIncome = profile.TypeIncome,
                PaymentType = profile.PaymentType
            },
            Issuer = MapIssuer(sale, issuer),
            Buyer = MapBuyer(sale, profile),
            Totals = EcfTotalsCalculator.Calculate(items),
            Items = items,
            SignedAt = issuance.SignedAt
        };
    }

    private static IReadOnlyList<EcfItem> MapItems(Sale sale, EcfTypeProfile profile)
    {
        var items = new List<EcfItem>();
        var lineNumber = 0;

        foreach (var item in sale.Items)
        {
            lineNumber++;

            items.Add(new EcfItem
            {
                LineNumber = lineNumber,
                TaxTreatment = item.TaxCode,
                Name = item.ProductName,
                Kind = profile.ItemKind,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                Amount = item.Subtotal
            });
        }

        return items;
    }

    private static EcfIssuer MapIssuer(Sale sale, Company issuer)
    {
        var legalName = string.IsNullOrWhiteSpace(issuer.LegalName) ? issuer.Name : issuer.LegalName;

        return new EcfIssuer
        {
            TaxId = issuer.TaxId,
            BusinessName = legalName,
            TradeName = issuer.Name == legalName ? null : issuer.Name,
            Address = issuer.Address,
            PhoneNumbers = string.IsNullOrWhiteSpace(issuer.Phone)
                ? Array.Empty<string>()
                : new[] { issuer.Phone },
            Email = string.IsNullOrWhiteSpace(issuer.Email) ? null : issuer.Email,
            IssuedAt = sale.CreatedAt
        };
    }

    private static EcfBuyer? MapBuyer(Sale sale, EcfTypeProfile profile)
    {
        var taxId = string.IsNullOrWhiteSpace(sale.ReceptorTaxId) ? null : sale.ReceptorTaxId;
        var name = string.IsNullOrWhiteSpace(sale.ClienteName) ? null : sale.ClienteName;

        if (profile.RequiresBuyerTaxId && taxId is null)
            throw new InvalidOperationException(
                $"The e-CF {profile.DocumentType} requires the tax id of the buyer.");

        if (profile.RequiresBuyerName && name is null)
            throw new InvalidOperationException(
                $"The e-CF {profile.DocumentType} requires the name of the buyer.");

        if (!profile.RequiresBuyer && taxId is null && name is null)
            return null;

        return new EcfBuyer
        {
            TaxId = taxId,
            BusinessName = name
        };
    }
}
