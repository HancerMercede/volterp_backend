using Volterp.ElectronicInvoicing.Schemas;

namespace Volterp.ElectronicInvoicing.Xml;

/// <summary>
/// What one type of document requires and the defaults that fit it. The
/// requirements are read from the official schema, so a type never gets the
/// rules of another one.
/// </summary>
public sealed record EcfTypeProfile
{
    /// <summary>The type this profile describes.</summary>
    public required EcfDocumentType DocumentType { get; init; }

    /// <summary>The kind of income the documents of this type declare.</summary>
    public required EcfTypeIncome TypeIncome { get; init; }

    /// <summary>How the buyer pays for the documents of this type.</summary>
    public required EcfPaymentType PaymentType { get; init; }

    /// <summary>Whether the lines of this type sell goods or services.</summary>
    public required EcfItemKind ItemKind { get; init; }

    /// <summary>Whether the type requires the expiry of the authorized sequence.</summary>
    public required bool RequiresSequenceExpiry { get; init; }

    /// <summary>Whether the type requires the buyer.</summary>
    public required bool RequiresBuyer { get; init; }

    /// <summary>Whether the type requires the tax id of the buyer.</summary>
    public required bool RequiresBuyerTaxId { get; init; }

    /// <summary>Whether the type requires the name of the buyer.</summary>
    public required bool RequiresBuyerName { get; init; }
}

/// <summary>
/// The profiles of the types of document, built from the official schemas.
/// </summary>
public static class EcfTypeProfiles
{
    /// <summary>The profile of a type of document.</summary>
    public static EcfTypeProfile For(EcfDocumentType documentType)
    {
        var schema = EcfSchemas.For(documentType);

        return new EcfTypeProfile
        {
            DocumentType = documentType,
            TypeIncome = EcfTypeIncome.Operations,
            PaymentType = EcfPaymentType.Cash,
            ItemKind = EcfItemKind.Goods,
            RequiresSequenceExpiry = schema.IsRequired("Encabezado/IdDoc/FechaVencimientoSecuencia"),
            RequiresBuyer = schema.IsRequired("Encabezado/Comprador"),
            RequiresBuyerTaxId = schema.IsRequired("Encabezado/Comprador/RNCComprador"),
            RequiresBuyerName = schema.IsRequired("Encabezado/Comprador/RazonSocialComprador")
        };
    }
}
