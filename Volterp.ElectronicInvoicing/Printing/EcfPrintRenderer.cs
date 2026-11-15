using System.Globalization;
using System.Text;
using Volterp.ElectronicInvoicing.Xml;

namespace Volterp.ElectronicInvoicing.Printing;

// The printed representation of an e-CF: the paper the receiver keeps when
// it is not electronic.
//
// The layout is its own, taken from the official spec ("Informe Técnico
// e-CF v1.0", section 18), and it is not a copy of the XML: it carries the
// data the spec asks for, the QR and the security code.
//
// The representation is HTML and not a PDF: the browser prints the page and
// the PDF is the one the browser writes when it prints it. Writing the PDF
// here would add a dependency for a format the browser already produces.
public static class EcfPrintRenderer
{
    // The lines that fit on a page of the representation. The spec does not
    // fix it: the layout of the page does.
    private const int LinesPerPage = 20;

    // The margin of the sheet leaves the two centimeters the spec asks for
    // between the edge of the paper and the QR.
    private const string Styles = """
        @page { size: letter; margin: 20mm; }
        * { box-sizing: border-box; }
        body { margin: 0; font-family: Arial, Helvetica, sans-serif; font-size: 10pt; color: #111; }
        .ri { max-width: 190mm; margin: 0 auto; }
        .page { position: relative; min-height: 232mm; page-break-after: always; break-after: page; }
        .page:last-child { page-break-after: auto; break-after: auto; }
        header { display: flex; justify-content: space-between; border-bottom: 2px solid #111; padding-bottom: 6px; }
        .header-left { width: 56%; }
        .header-right { width: 40%; text-align: right; }
        .issuer-name { font-size: 14pt; font-weight: 700; }
        .trade-name { font-size: 11pt; }
        .doc-type { font-size: 11.5pt; font-weight: 700; text-transform: uppercase; }
        .buyer { margin: 6px 0 8px; }
        table { width: 100%; border-collapse: collapse; }
        .items th, .items td { border: 1px solid #8c8c8c; padding: 2px 4px; font-size: 9.5pt; }
        .items th { background: #f2f2f2; text-align: left; }
        .items .number, .items .exempt { width: 3%; text-align: center; }
        .amount { text-align: right; white-space: nowrap; }
        .totals { width: 46%; margin: 10px 0 0 auto; }
        .totals td, .totals th { padding: 2px 6px; }
        .totals th { text-align: right; }
        footer { position: absolute; bottom: 0; left: 0; right: 0; display: flex; justify-content: space-between; align-items: flex-end; border-top: 1px solid #8c8c8c; padding-top: 6px; }
        .qr-block { font-size: 9.5pt; }
        .qr { width: 32mm; height: 32mm; padding: 3mm; }
        .qr svg { width: 100%; height: 100%; display: block; }
        .security { font-weight: 700; letter-spacing: 1px; }
        .foot-right { text-align: right; font-size: 9pt; color: #333; }
        .pagination { margin-bottom: 4px; }
        .url { word-break: break-all; max-width: 90mm; }
        """;

    public static string Render(EcfPrintRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var document = request.Document;
        var template = EcfPrintTemplates.For(document);
        var pages = PagesOf(document, request.Pagination);

        var html = new StringBuilder();

        html.Append("<!DOCTYPE html><html lang=\"es\"><head><meta charset=\"utf-8\" />");
        html.Append("<title>Representación impresa ").Append(Text(document.Identity.ENCF)).Append("</title>");
        html.Append("<style>").Append(Styles).Append("</style></head><body>");
        html.Append("<main class=\"ri\" data-template=\"").Append(EcfPrintTemplates.SlugOf(template)).Append("\">");

        foreach (var page in pages)
            AppendPage(html, request, page, pages.Count);

        html.Append("</main></body></html>");

        return html.ToString();
    }

    // The representation may take more than one page: the lines are split in
    // pages and the last one carries the totals of the document.
    private static IReadOnlyList<Page> PagesOf(EcfDocument document, EcfPrintPagination? pagination)
    {
        if (pagination is not null)
            return DeclaredPagesOf(document.Items, pagination);

        var pages = new List<Page>();
        var items = document.Items;

        if (items.Count == 0)
        {
            pages.Add(new Page(1, items, true, null));
            return pages;
        }

        var number = 1;

        for (var start = 0; start < items.Count; start += LinesPerPage)
        {
            var lines = items.Skip(start).Take(LinesPerPage).ToList();

            pages.Add(new Page(number, lines, start + LinesPerPage >= items.Count, null));
            number++;
        }

        return pages;
    }

    private static IReadOnlyList<Page> DeclaredPagesOf(
        IReadOnlyList<EcfItem> items,
        EcfPrintPagination pagination)
    {
        if (pagination.TotalPages.HasValue && pagination.TotalPages.Value != pagination.Pages.Count)
            throw new InvalidOperationException("The pagination of the document declares a different number of pages.");

        var pages = new List<Page>();
        var expectedLine = items.Count == 0 ? 1 : items[0].LineNumber;

        foreach (var declared in pagination.Pages)
        {
            if (declared.Number != pages.Count + 1)
                throw new InvalidOperationException("The pagination of the document is not in order.");
            if (declared.FirstLineNumber != expectedLine)
                throw new InvalidOperationException("The lines of the document do not follow the order of its pages.");

            var lines = items
                .Where(item => item.LineNumber >= declared.FirstLineNumber && item.LineNumber <= declared.LastLineNumber)
                .ToList();

            if (lines.Count != declared.LastLineNumber - declared.FirstLineNumber + 1)
                throw new InvalidOperationException("A page of the document names lines that the document does not have.");

            pages.Add(new Page(declared.Number, lines, pages.Count == pagination.Pages.Count - 1, declared));
            expectedLine = declared.LastLineNumber + 1;
        }

        if (pages.Count == 0)
            throw new InvalidOperationException("The document declares a pagination without pages.");
        if (items.Count > 0 && expectedLine != items[^1].LineNumber + 1)
            throw new InvalidOperationException("The pagination of the document does not cover all of its lines.");

        return pages;
    }

    private static void AppendPage(StringBuilder html, EcfPrintRequest request, Page page, int totalPages)
    {
        html.Append("<section class=\"page\">");

        AppendHeader(html, request.Document);
        AppendBuyer(html, request.Document);
        AppendItems(
            html,
            request.Document,
            page.Items,
            request.AdditionalTaxes,
            request.ItemAmountsIncludeItbis);

        if (page.IsLast)
            AppendTotals(html, request.Document, request.AdditionalTaxes);

        AppendFooter(html, request, page, totalPages);

        html.Append("</section>");
    }

    // The header carries the emisor on the left and the document on the
    // right ("Informe Técnico e-CF v1.0", section 18.2.1).
    private static void AppendHeader(StringBuilder html, EcfDocument document)
    {
        var issuer = document.Issuer;

        html.Append("<header>");

        html.Append("<div class=\"header-left\">");

        if (!string.IsNullOrWhiteSpace(issuer.TradeName))
            html.Append("<div class=\"trade-name\">").Append(Text(issuer.TradeName)).Append("</div>");

        html.Append("<div class=\"issuer-name\">").Append(Text(issuer.BusinessName)).Append("</div>");

        if (!string.IsNullOrWhiteSpace(issuer.Branch))
            html.Append("<div>Sucursal: ").Append(Text(issuer.Branch)).Append("</div>");

        html.Append("<div>RNC: ").Append(Text(issuer.TaxId)).Append("</div>");

        if (!string.IsNullOrWhiteSpace(issuer.Address))
            html.Append("<div>").Append(Text(issuer.Address)).Append("</div>");

        if (!string.IsNullOrWhiteSpace(issuer.Municipality) || !string.IsNullOrWhiteSpace(issuer.Province))
            html.Append("<div>").Append(Text(Join(issuer.Municipality, issuer.Province))).Append("</div>");

        html.Append("<div>Fecha de emisión: ").Append(Day(issuer.IssuedAt)).Append("</div>");

        html.Append("</div>");

        html.Append("<div class=\"header-right\">");
        html.Append("<div class=\"doc-type\">").Append(Text(EcfDocumentTypeNames.Of(document.DocumentType))).Append("</div>");
        html.Append("<div>e-NCF: ").Append(Text(document.Identity.ENCF)).Append("</div>");

        // The expiry of the authorized sequence: the date the e-NCF expires.
        if (document.Identity.SequenceExpiresAt is not null)
            html.Append("<div>Fecha Vencimiento: ").Append(Day(document.Identity.SequenceExpiresAt)).Append("</div>");

        if (IsNote(document.DocumentType))
        {
            var reference = document.Reference
                ?? throw new InvalidOperationException("The note carries no reference to the document it modifies.");

            if (string.IsNullOrWhiteSpace(reference.ModifiedENCF)
                || reference.ModifiedAt is null
                || reference.ModificationCode is null)
                throw new InvalidOperationException("The note carries no complete reference to the document it modifies.");

            html.Append("<div>e-NCF Modificado: ").Append(Text(reference.ModifiedENCF)).Append("</div>");
            html.Append("<div>Fecha NCF Modificado: ").Append(Day(reference.ModifiedAt)).Append("</div>");

            if (!string.IsNullOrWhiteSpace(reference.OtherContributorTaxId))
                html.Append("<div>RNC Otro Contribuyente: ").Append(Text(reference.OtherContributorTaxId)).Append("</div>");

            html.Append("<div>Código de Modificación: ")
                .Append(Text(EcfModificationCodeDescriptions.Of(reference.ModificationCode.Value)))
                .Append("</div>");

            if (!string.IsNullOrWhiteSpace(reference.ModificationReason))
                html.Append("<div>Razón de Modificación: ").Append(Text(reference.ModificationReason)).Append("</div>");
        }

        html.Append("</div>");
        html.Append("</header>");
    }

    // The buyer goes at the lower left of the header. The simplified layout
    // of the final consumer has no buyer block.
    private static void AppendBuyer(StringBuilder html, EcfDocument document)
    {
        if (EcfPrintTemplates.For(document) != EcfPrintTemplate.RegisteredBuyer)
            return;

        var buyer = document.Buyer;

        if (buyer is null)
            return;

        html.Append("<div class=\"buyer\">");

        if (!string.IsNullOrWhiteSpace(buyer.BusinessName))
            html.Append("<div>").Append(Text(buyer.BusinessName)).Append("</div>");

        if (!string.IsNullOrWhiteSpace(buyer.TaxId))
            html.Append("<div>RNC: ").Append(Text(buyer.TaxId)).Append("</div>");

        html.Append("</div>");
    }

    private static void AppendItems(
        StringBuilder html,
        EcfDocument document,
        IReadOnlyList<EcfItem> items,
        EcfPrintAdditionalTaxes? additionalTaxes,
        bool amountsIncludeItbis)
    {
        html.Append("<table class=\"items\"><thead><tr>");
        html.Append("<th class=\"number\">No.</th>");
        html.Append("<th class=\"amount\">Cantidad</th>");
        html.Append("<th class=\"exempt\"></th>");
        html.Append("<th>Descripción</th>");
        html.Append("<th>Unidad de Medida</th>");
        html.Append("<th class=\"amount\">Precio</th>");
        html.Append("<th class=\"amount\">ISC Específico</th>");
        html.Append("<th class=\"amount\">ISC Ad-Valorem</th>");
        html.Append("<th class=\"amount\">ITBIS</th>");
        html.Append("<th class=\"amount\">Descuento</th>");
        html.Append("<th class=\"amount\">Recargo</th>");
        html.Append("<th class=\"amount\">Valor</th>");
        html.Append("</tr></thead><tbody>");

        foreach (var item in items)
        {
            html.Append("<tr>");
            html.Append("<td class=\"number\">").Append(Number(item.LineNumber)).Append("</td>");
            html.Append("<td class=\"amount\">").Append(Number(item.Quantity)).Append("</td>");

            // The indicator of the exempt lines goes to the left of the
            // description of the item.
            html.Append("<td class=\"exempt\">")
                .Append(item.TaxTreatment == TaxCode.Exento ? "E" : string.Empty)
                .Append("</td>");

            html.Append("<td>").Append(Text(DescriptionOf(item))).Append("</td>");
            html.Append("<td>").Append(Text(UnitOf(item))).Append("</td>");
            html.Append("<td class=\"amount\">").Append(Money(item.UnitPrice)).Append("</td>");
            html.Append("<td class=\"amount\">").Append(Money(LineTax(
                additionalTaxes,
                item.LineNumber,
                entry => entry.SelectiveConsumptionTax))).Append("</td>");
            html.Append("<td class=\"amount\">").Append(Money(LineTax(
                additionalTaxes,
                item.LineNumber,
                entry => entry.AdValoremSelectiveConsumptionTax))).Append("</td>");
            html.Append("<td class=\"amount\">").Append(Money(ItbisOf(
                item,
                document.Adjustments,
                amountsIncludeItbis))).Append("</td>");
            html.Append("<td class=\"amount\">").Append(Money(item.DiscountAmount)).Append("</td>");
            html.Append("<td class=\"amount\">").Append(Money(item.SurchargeAmount)).Append("</td>");
            html.Append("<td class=\"amount\">").Append(Money(item.Amount)).Append("</td>");
            html.Append("</tr>");
        }

        html.Append("</tbody></table>");
    }

    private static void AppendTotals(StringBuilder html, EcfDocument document, EcfPrintAdditionalTaxes? additionalTaxes)
    {
        var totals = document.Totals;

        html.Append("<table class=\"totals\">");

        TextRow(html, "Forma de pago", PaymentOf(document.Identity.PaymentType));
        TextRow(html, "Condición de pago", document.Identity.PaymentTerm);
        TextRow(html, "Fecha límite de pago", Day(document.Identity.PaymentDueDate));
        Total(html, "Subtotal Gravado", totals.TaxableTotal);
        Total(html, "Subtotal Exento", totals.ExemptTotal);
        Total(html, "Monto no facturable", totals.NonBillableTotal);
        Total(
            html,
            "Total ISC",
            additionalTaxes is null ? totals.AdditionalTaxTotal : additionalTaxes.SelectiveConsumptionTax);
        Total(html, "Total ITBIS", totals.ItbisTotal);
        Total(html, "CDT", additionalTaxes?.TelecomDevelopmentTax);
        Total(html, "Propina legal", additionalTaxes?.LegalTip);
        Total(html, "ITBIS retenido", totals.ItbisWithheld);
        Total(html, "ISR retención", totals.IncomeTaxWithheld);
        Total(html, "Monto periodo", totals.PeriodTotal);
        Total(html, "Saldo anterior", totals.PreviousBalance);
        Total(html, "Monto avance de pago", totals.PaymentAdvance);
        Total(html, "Valor a pagar", totals.AmountToPay);

        foreach (var adjustment in document.Adjustments)
        {
            TextRow(html, "Descripción de Descuento o Recargo", adjustment.Description);

            if (adjustment.ValueKind == EcfAdjustmentValueKind.Percentage && adjustment.Value is not null)
                TextRow(html, "Descuento o Recargo en %", $"{Money(adjustment.Value)}%");

            Total(html, "Monto de Descuento o Recargo", adjustment.Amount);
        }

        html.Append("<tr><th>Total</th><th class=\"amount\">").Append(Money(totals.Total)).Append("</th></tr>");
        html.Append("</table>");
    }

    private static void Total(StringBuilder html, string label, decimal? amount)
    {
        if (amount is null)
            return;

        html.Append("<tr><td>").Append(Text(label)).Append("</td><td class=\"amount\">")
            .Append(Money(amount)).Append("</td></tr>");
    }

    private static void TextRow(StringBuilder html, string label, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        html.Append("<tr><td>").Append(Text(label)).Append("</td><td>").Append(Text(text)).Append("</td></tr>");
    }

    private static string PaymentOf(EcfPaymentType? payment) => payment switch
    {
        EcfPaymentType.Cash => "Contado",
        EcfPaymentType.Credit => "Crédito",
        EcfPaymentType.Free => "Gratis",
        _ => string.Empty
    };

    // The QR goes at the lower left of the representation, with the security
    // code and the date of the signature below it.
    private static void AppendFooter(StringBuilder html, EcfPrintRequest request, Page page, int totalPages)
    {
        html.Append("<footer>");

        html.Append("<div class=\"qr-block\">");
        html.Append("<div class=\"qr\">").Append(QrMarkup(request.QrImageSvg)).Append("</div>");
        html.Append("<div class=\"security\">Código de Seguridad: ").Append(Text(request.SecurityCode)).Append("</div>");
        html.Append("<div>Fecha de Firma Digital: ").Append(Moment(request.Document.SignedAt)).Append("</div>");
        html.Append("</div>");

        html.Append("<div class=\"foot-right\">");

        // The pagination only applies when the representation has more than
        // one page, and it is not on the last one: that page carries the
        // totals of the whole document.
        if (totalPages > 1 && !page.IsLast)
        {
            var totals = PageTotalsOf(request.Document, page);

            html.Append("<div class=\"pagination\">");
            html.Append("<div>Página ").Append(page.Number).Append(" de ").Append(totalPages).Append("</div>");
            PageTotal(html, "Subtotal Gravado Página", totals.Taxable);
            PageTotal(html, "Subtotal Exento Página", totals.Exempt);
            PageTotal(html, "Subtotal Impuesto Selectivo al Consumo Página", totals.SelectiveConsumptionTax);
            PageTotal(html, "Subtotal Otros Impuestos Adicionales Página", totals.OtherAdditionalTaxes);
            PageTotal(html, "Monto total Página", totals.Total);
            PageTotal(html, "Subtotal ITBIS Página", totals.Itbis);
            PageTotal(html, "Subtotal Impuesto Adicional Página", totals.AdditionalTax);
            html.Append("</div>");
        }

        html.Append("<div class=\"url\">").Append(Text(request.QrUrl)).Append("</div>");
        html.Append("</div>");

        html.Append("</footer>");
    }

    // The image of the QR travels inside the representation, so the paper does
    // not depend on the browser reaching the server to draw it.
    private static string QrMarkup(string svg)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(svg);

        var start = svg.IndexOf("<svg", StringComparison.Ordinal);

        if (start < 0)
            throw new InvalidOperationException("The image of the QR is not an SVG.");

        return svg[start..];
    }

    private static bool IsNote(EcfDocumentType documentType)
        => documentType is EcfDocumentType.DebitNote or EcfDocumentType.CreditNote;

    // IndicadorMontoGravado 0 means the line amount excludes ITBIS; 1 means
    // it already includes ITBIS and the tax must be extracted from it.
    private static decimal ItbisOf(
        EcfItem item,
        IReadOnlyList<EcfAdjustment> adjustments,
        bool amountsIncludeItbis)
    {
        var amount = item.Amount;

        foreach (var adjustment in adjustments.Where(adjustment => adjustment.LineNumber == item.LineNumber))
        {
            var value = adjustment.Amount ?? 0m;
            amount += adjustment.Kind == EcfAdjustmentKind.Discount ? -value : value;
        }

        var rate = EcfTaxRates.For(item.TaxTreatment).Fraction;
        var tax = amountsIncludeItbis
            ? amount * rate / (1m + rate)
            : amount * rate;

        return decimal.Round(tax, 2, MidpointRounding.AwayFromZero);
    }

    private static PageTotals PageTotalsOf(EcfDocument document, Page page)
    {
        if (page.Declared is not null)
        {
            return new PageTotals(
                page.Declared.TaxableSubtotal,
                page.Declared.ExemptSubtotal,
                page.Declared.ItbisSubtotal,
                page.Declared.SelectiveConsumptionTaxSubtotal,
                page.Declared.OtherAdditionalTaxesSubtotal,
                page.Declared.AdditionalTaxSubtotal,
                page.Declared.Total);
        }

        var adjustments = document.Adjustments
            .Where(adjustment => page.Items.Any(item => item.LineNumber == adjustment.LineNumber));
        var totals = EcfTotalsCalculator.Calculate(page.Items, adjustments);

        return new PageTotals(
            totals.TaxableTotal,
            totals.ExemptTotal,
            totals.ItbisTotal,
            null,
            null,
            null,
            totals.Total);
    }

    private static void PageTotal(StringBuilder html, string label, decimal? amount)
    {
        if (amount is null)
            return;

        html.Append("<div>").Append(Text(label)).Append(": ").Append(Money(amount)).Append("</div>");
    }

    private static decimal? LineTax(
        EcfPrintAdditionalTaxes? additionalTaxes,
        int lineNumber,
        Func<EcfPrintAdditionalTaxEntry, decimal?> amountOf)
    {
        if (additionalTaxes is null)
            return null;

        var amounts = additionalTaxes.Entries
            .Where(entry => entry.LineNumber == lineNumber)
            .Select(amountOf)
            .Where(amount => amount is not null)
            .Select(amount => amount!.Value)
            .ToList();

        return amounts.Count == 0 ? null : amounts.Sum();
    }

    private static string DescriptionOf(EcfItem item)
        => item.Name;

    private static string UnitOf(EcfItem item)
        => item.UnitOfMeasure is int code
            ? EcfUnitsOfMeasure.Of(code) ?? code.ToString(CultureInfo.InvariantCulture)
            : string.Empty;

    private static string Join(string? first, string? second)
        => string.Join(", ", new[] { first, second }.Where(part => !string.IsNullOrWhiteSpace(part)));

    private static string Text(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        // Only the characters the HTML gives a meaning to are escaped: the
        // page is UTF-8 and says so, so the accents of the document stay
        // readable in the representation.
        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");
    }

    private static string Money(decimal? value)
        => value?.ToString("#,##0.00", CultureInfo.InvariantCulture) ?? string.Empty;

    private static string Number(decimal value)
        => value.ToString("#,##0.##", CultureInfo.InvariantCulture);

    private static string Day(DateTime? value)
        => value?.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture) ?? string.Empty;

    private static string Moment(DateTime value)
        => value.ToString("dd-MM-yyyy HH:mm:ss", CultureInfo.InvariantCulture);

    private sealed record Page(
        int Number,
        IReadOnlyList<EcfItem> Items,
        bool IsLast,
        EcfPrintPage? Declared);

    private sealed record PageTotals(
        decimal? Taxable,
        decimal? Exempt,
        decimal? Itbis,
        decimal? SelectiveConsumptionTax,
        decimal? OtherAdditionalTaxes,
        decimal? AdditionalTax,
        decimal? Total);
}
