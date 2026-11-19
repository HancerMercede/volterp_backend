using Volterp.ElectronicInvoicing;
using Volterp.ElectronicInvoicing.Xml;

namespace Volterp.ElectronicInvoicing.Tests.Dgii;

// The document the circuit sends to the DGII: a credit fiscal (31) built
// the same way the ERP builds one, so what arrives is a document the DGII
// can judge instead of a sketch that it would reject for its shape.
//
// The issuer's RNC and the number of the range are the taxpayer's: they
// come from the environment and never from the repository.
internal static class TestEcfDocument
{
    // The pre-certification ranges of the DGII expire on this date.
    public static readonly DateTime SequenceExpiresAt = new(2028, 12, 31);

    // The buyer of the test document: a valid RNC that says nothing about
    // anybody's data.
    private const string BuyerRnc = "101500018";

    public static string Build(string issuerRnc, string encf)
    {
        var profile = EcfTypeProfiles.For(EcfDocumentType.FiscalCredit);

        var items = new[]
        {
            new EcfItem
            {
                LineNumber = 1,
                TaxTreatment = TaxCode.ItbisTasa2,
                Name = "Prueba de circuito contra la DGII",
                Kind = profile.ItemKind,
                Quantity = 1m,
                UnitPrice = 100m,
                Amount = 100m
            }
        };

        return new EcfXmlWriter().Write(new EcfDocument
        {
            DocumentType = EcfDocumentType.FiscalCredit,
            Identity = new EcfIdentity
            {
                ENCF = encf,
                SequenceExpiresAt = SequenceExpiresAt,
                TypeIncome = profile.TypeIncome,
                PaymentType = profile.PaymentType
            },
            Issuer = new EcfIssuer
            {
                TaxId = issuerRnc,
                BusinessName = "Contribuyente de prueba",
                Address = "Dirección de prueba",
                IssuedAt = DateTime.UtcNow
            },
            Buyer = new EcfBuyer
            {
                TaxId = BuyerRnc,
                BusinessName = "Comprador de prueba"
            },
            Totals = EcfTotalsCalculator.Calculate(items),
            Items = items,
            SignedAt = DateTime.UtcNow
        });
    }
}
