using Volterp.Domain.Entities;
using Volterp.ElectronicInvoicing;
using Volterp.ElectronicInvoicing.Schemas;
using Volterp.ElectronicInvoicing.Xml;

namespace Volterp.Application.Mappers;

public sealed record EcfIssuance
{
    public required string ENCF { get; init; }

    public DateTime? SequenceExpiresAt { get; init; }

    public required DateTime SignedAt { get; init; }

    // The paper document this e-CF replaces: its number and its date are the
    // reference the norm asks for.
    public EcfReplacement? Replacement { get; init; }
}

// The document on paper that an e-CF replaces. The number and the day it
// was issued on are what the reference of the replacement carries.
public sealed record EcfReplacement(string Number, DateTime IssuedAt);

// The identity that issues the document: the RNC of the issuer and the
// legal name the DGII reads. A branch of the tenant issues with its own RNC
// and its own legal name.
public sealed record IssuerIdentity(string TaxId, string LegalName);

public static class SaleEcfDocumentMapper
{
    public static EcfDocument Map(Sale sale, Company company, IssuerIdentity issuer, EcfIssuance issuance)
    {
        ArgumentNullException.ThrowIfNull(sale);
        ArgumentNullException.ThrowIfNull(company);
        ArgumentNullException.ThrowIfNull(issuer);
        ArgumentNullException.ThrowIfNull(issuance);

        if (sale.Items.Count == 0)
            throw new InvalidOperationException("The sale has no lines to issue.");

        if (sale.CompanyId != company.Id)
            throw new InvalidOperationException(
                "The sale does not belong to the company that issues the document.");

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
                CreditNoteBeyondThirtyDays = MapCreditNoteBeyondThirtyDays(sale, issuance.SignedAt),
                TypeIncome = profile.TypeIncome,
                PaymentType = profile.PaymentType
            },
            Issuer = MapIssuer(sale, company, issuer),
            Buyer = MapBuyer(sale, profile),
            Totals = EcfTotalsCalculator.Calculate(items),
            Items = items,
            Reference = MapReference(sale, issuance),
            SignedAt = issuance.SignedAt
        };
    }

    private static bool? MapCreditNoteBeyondThirtyDays(Sale sale, DateTime signedAt)
    {
        if (sale.DocumentType != EcfDocumentType.CreditNote || sale.ModifiedAt is null)
            return null;

        return signedAt.Date > sale.ModifiedAt.Value.Date.AddDays(30);
    }

    private static EcfReference? MapReference(Sale sale, EcfIssuance issuance)
    {
        // The replacement of a paper document carries its reference even when
        // the schema of the type does not ask for it: the norm says the
        // e-CF identifies the document it replaces.
        if (issuance.Replacement is not null)
        {
            return new EcfReference
            {
                ModifiedENCF = issuance.Replacement.Number,
                ModifiedAt = issuance.Replacement.IssuedAt,
                ModificationCode = EcfModificationCode.ReplacesContingencyNcf
            };
        }

        if (!EcfSchemas.For(sale.DocumentType).IsRequired("InformacionReferencia"))
            return null;

        if (!sale.HasDocumentReference)
            throw new InvalidOperationException(
                $"The e-CF {sale.DocumentType} requires the document it modifies.");

        return new EcfReference
        {
            ModifiedENCF = sale.ModifiedENCF,
            OtherContributorTaxId = sale.ReceptorTaxId,
            ModifiedAt = sale.ModifiedAt,
            ModificationCode = sale.ModificationCode,
            ModificationReason = sale.ModificationReason
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

    private static EcfIssuer MapIssuer(Sale sale, Company company, IssuerIdentity issuer)
    {
        var legalName = string.IsNullOrWhiteSpace(issuer.LegalName)
            ? string.IsNullOrWhiteSpace(company.LegalName) ? company.Name : company.LegalName
            : issuer.LegalName;

        return new EcfIssuer
        {
            TaxId = issuer.TaxId,
            BusinessName = legalName,
            TradeName = company.Name == legalName ? null : company.Name,
            Address = company.Address,
            PhoneNumbers = string.IsNullOrWhiteSpace(company.Phone)
                ? Array.Empty<string>()
                : new[] { company.Phone },
            Email = string.IsNullOrWhiteSpace(company.Email) ? null : company.Email,
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
