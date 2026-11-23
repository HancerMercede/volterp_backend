using EitherWay;
using Volterp.Application.DTOs.ElectronicInvoiceDtos;
using Volterp.Application.DTOs.NormalFiscalDocumentDtos;
using Volterp.Application.DTOs.SaleDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.Domain.Policies;
using Volterp.ElectronicInvoicing;

namespace Volterp.Application.Services;

public class SaleService(
    IUnitOfWork unitOfWork,
    IElectronicInvoiceService electronicInvoices,
    INormalFiscalDocumentService normalDocuments,
    TimeProvider? timeProvider = null) : ISaleService
{
    private const string CodeAlreadyInUseMessage = "The code is already in use.";
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private readonly INormalFiscalDocumentService _normalDocuments = normalDocuments;

    public async Task<PagedResult<SaleDto>> GetAllSalesAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var sales = await unitOfWork.Sales.GetAllSalesAsync(pageNumber, pageSize, ct);
        return sales.MapTo<Sale, SaleDto>();
    }

    public async Task<PagedResult<SaleDto>> GetSalesByStatusAsync(SaleStatus status, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var sales = await unitOfWork.Sales.GetSalesByStatusAsync(status, pageNumber, pageSize, ct);
        return sales.MapTo<Sale, SaleDto>();
    }

    public async Task<Either<Error, SaleDto?>> GetSaleByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await EitherAsync<Error, Guid>
            .FromRight(id)
            .Ensure(x => x != Guid.Empty, new Error("id must not be empty."))
            .FlatMap(async saleId => await unitOfWork.Sales.GetSaleByIdAsync(saleId, ct),
                ex => new Error(ex.Message))
            .Map(sale => sale?.MapTo<Sale, SaleDto>())
            .Run();
    }

    public async Task<Either<Error, SaleDto>> CreateSaleAsync(CreateSaleRequest request, Guid companyId, Guid? userId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, CreateSaleRequest>
            .FromRight(request)
            .Ensure(r => r.Items.Count > 0, new Error("Sale must have at least one item."))
            .FlatMap(async req =>
            {
                var sale = req.Project();
                sale.CompanyId = companyId;
                if (userId.HasValue) sale.SetCreatedBy(userId.Value);

                // The issuer is resolved first: a rejected issuer must not
                // burn a code.
                sale.IssuerTaxId = await ResolveIssuerTaxIdAsync(req.IssuerTaxId, companyId, ct);

                // The code is resolved before any stock movement: a rejected code
                // must not leave the inventory half-updated.
                sale.Code = await ResolveCodeForCreateAsync(req.Code, companyId, ct);

                // Batch query: single SELECT for all products (avoids N+1)
                var productIds = sale.Items.Select(i => i.ProductId).ToHashSet();
                var products = await unitOfWork.Products.GetProductsByIdsAsync(productIds, ct);
                var productMap = products.ToDictionary(p => p.Id);

                // Validate and deduct stock for each item
                // NOTE: Stock is deducted at creation time to reserve inventory.
                // The completion turns the reservation into a sale: it does not
                // deduct the stock again.
                foreach (var item in sale.Items)
                {
                    if (!productMap.TryGetValue(item.ProductId, out var product))
                    {
                        // The tax code comes from the product. Without it the line
                        // cannot be taxed, so it is declared not taxable instead of
                        // inventing a rate.
                        item.TaxCode = TaxCode.NoFacturable;
                        continue;
                    }

                    product.DeductStock(item.Quantity);
                    item.TaxCode = product.TaxCode;
                }

                await ResolveReceptorTaxIdAsync(sale, ct);
                sale.CalculateTotals();

                await unitOfWork.Sales.AddSaleAsync(sale, ct);
                await unitOfWork.CommitAsync(ct);
                return sale;
            }, ex => new Error(ex.Message))
            .Map(sale =>sale.MapTo<Sale, SaleDto>())
            .Run();
    }

    public async Task<Either<Error, SaleDto>> UpdateSaleAsync(Guid id, Guid companyId, UpdateSaleRequest request, Guid? userId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, Guid>
            .FromRight(id)
            .Ensure(x => x != Guid.Empty, new Error("id must not be empty."))
            .FlatMap(async saleId => await unitOfWork.Sales.GetSaleByIdAsync(saleId, ct),
                ex => new Error(ex.Message))
            .Ensure(sale => sale is not null, new Error("Sale not found.", ErrorKind.NotFound))
            .Ensure(_ => request.Status != SaleStatus.Completed,
                new Error("A sale can only be completed through the completion operation."))
            .Ensure(sale => sale is not null && sale.Status != SaleStatus.Completed,
                new Error("A completed sale cannot be updated."))
            .FlatMap(async sale =>
            {
                // The code is resolved before any stock movement: a rejected code
                // must not leave the inventory half-updated.
                var code = await ResolveCodeForUpdateAsync(request.Code, sale.Code, companyId, sale.Id, ct);

                // The sale is issued by the RNC it names: it has to be one of
                // the issuers of the tenant. An empty one keeps the issuer
                // the sale already had.
                var issuerTaxId = string.IsNullOrWhiteSpace(request.IssuerTaxId)
                    ? sale.IssuerTaxId
                    : await ResolveIssuerTaxIdAsync(request.IssuerTaxId, companyId, ct);

                // Collect all product IDs (old items + new items)
                var allProductIds = sale.Items.Select(i => i.ProductId)
                    .Concat(request.Items.Select(i => i.ProductId))
                    .ToHashSet();

                // Batch query: single SELECT for all products involved
                var products = await unitOfWork.Products.GetProductsByIdsAsync(allProductIds, ct);
                var productMap = products.ToDictionary(p => p.Id);

                // Build product name lookup for error messages. The same
                // product can be in more than one line of the sale: the lookup
                // keeps the first name and the lines are not merged.
                var newItemNames = request.Items
                    .GroupBy(i => i.ProductId)
                    .ToDictionary(g => g.Key, g => g.First().ProductName);

                // Apply old returns: restore stock from items being removed
                foreach (var item in sale.Items)
                {
                    if (productMap.TryGetValue(item.ProductId, out var product))
                        product.IncrementStock(item.Quantity);
                }

                // Validate and apply new deductions (group by productId to handle duplicates)
                var newDeductions = request.Items
                    .GroupBy(i => i.ProductId)
                    .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));

                foreach (var (productId, quantity) in newDeductions)
                {
                    if (!productMap.TryGetValue(productId, out var product))
                        continue;

                    product.DeductStock(quantity);
                }
                
                var newItems = request.Items.Map(i => i.Project());

                foreach (var item in newItems)
                {
                    item.TaxCode = productMap.TryGetValue(item.ProductId, out var product)
                        ? product.TaxCode
                        : TaxCode.NoFacturable;
                }

                sale.Apply(s =>
                {
                    s.ClienteId = request.ClienteId;
                    s.ClienteName = request.ClienteName;
                    s.Status = request.Status;
                    s.DocumentType = request.DocumentType;
                    s.Notes = request.Notes;
                    s.Code = code;
                    s.IssuerTaxId = issuerTaxId;
                    s.UpdatedAt = DateTime.UtcNow;
                    if (userId.HasValue) s.SetUpdatedBy(userId.Value);
                    s.Items = newItems;
                });

                await ResolveReceptorTaxIdAsync(sale, ct);
                sale.CalculateTotals();

                await unitOfWork.Sales.UpdateSaleAsync(sale, ct);
                await unitOfWork.CommitAsync(ct);
                return sale;
            }, ex => new Error(ex.Message))
            .Map(sale => sale.MapTo<Sale, SaleDto>())
            .Run();
    }

    public async Task<Either<Error, SaleDto>> CompleteSaleAsync(Guid id, Guid companyId, Guid? userId, bool asCommercialInvoice = false, CancellationToken ct = default)
    {
        // The emission can fail and the sale cannot be undone: the caller has
        // to learn that its fiscal document is not in good shape.
        var fiscalIssue = string.Empty;
        Guid? fiscalInvoiceId = null;
        EcfFiscalState? fiscalState = null;

        return await EitherAsync<Error, Guid>
            .FromRight(id)
            .Ensure(x => x != Guid.Empty, new Error("id must not be empty."))
            .FlatMap(async saleId => await unitOfWork.Sales.GetSaleByIdAsync(saleId, ct),
                ex => new Error(ex.Message))
            .Ensure(sale => sale is not null, new Error("Sale not found.", ErrorKind.NotFound))
            .FlatMap(async sale =>
            {
                sale.Complete();

                // The stock was deducted when the sale was created: that is
                // the reservation. Completing the sale turns the reservation
                // into a sale, it does not deduct the stock a second time.

                sale.Apply(s =>
                {
                    s.UpdatedAt = DateTime.UtcNow;
                    if (userId.HasValue) s.SetUpdatedBy(userId.Value);
                });

                await unitOfWork.Sales.UpdateSaleAsync(sale, ct);

                var now = _timeProvider.GetUtcNow().UtcDateTime;

                if (!CompanyEmissionPolicy.UsesElectronicEmission(sale.Company, now))
                {
                    // The commercial invoice: the sale leaves without a fiscal
                    // document, so it consumes no number and reaches no one.
                    if (asCommercialInvoice)
                    {
                        sale.IsCommercialInvoice = true;
                        await unitOfWork.CommitAsync(ct);

                        return sale;
                    }

                    // The paper document takes its number from the range the
                    // DGII authorized for the normal documents, and the
                    // number, the document and the completion of the sale land
                    // together. A document that could not be issued leaves the
                    // sale incomplete: the caller gets the reason.
                    var issued = await _normalDocuments.IssueAsync(sale.Id, companyId, userId, ct);

                    issued.Match<NormalFiscalDocumentDto>(
                        error => throw new InvalidOperationException(error.Message),
                        document => document);

                    // The taxpayer in contingency owes the DGII the
                    // replacement of the paper document: it is created next to
                    // it and it waits for the contingency to be lifted.
                    if (CompanyEmissionPolicy.IsInContingency(sale.Company, now))
                        (fiscalIssue, fiscalInvoiceId, fiscalState) = await IssueTheFiscalDocumentAsync(sale, companyId, ct);

                    return sale;
                }

                // The commercial invoice: the sale leaves without a fiscal
                // document, so it consumes no number and reaches no one. A
                // taxpayer that invoices electronically owes the DGII a
                // document for every sale: that hole is his to carry, and it
                // is not a reason to refuse the sale.
                if (asCommercialInvoice)
                {
                    sale.IsCommercialInvoice = true;
                    await unitOfWork.CommitAsync(ct);

                    return sale;
                }

                await unitOfWork.CommitAsync(ct);

                // The e-CF of the sale is queued here and the durable queue
                // sends it in the background. The sale is a commercial fact:
                // the fiscal document of the client cannot undo it, and the
                // document that could not be signed stays on record for a
                // human.
                (fiscalIssue, fiscalInvoiceId, fiscalState) = await IssueTheFiscalDocumentAsync(sale, companyId, ct);

                return sale;
            }, ex => new Error(ex.Message))
            .Map(sale =>
            {
                var dto = sale.MapTo<Sale, SaleDto>();

                dto.ElectronicInvoiceId = fiscalInvoiceId ?? dto.ElectronicInvoiceId;
                dto.FiscalState = fiscalState ?? dto.FiscalState;

                if (!string.IsNullOrWhiteSpace(fiscalIssue))
                    dto.FiscalNote = fiscalIssue;

                return dto;
            })
            .Run();
    }

    // What the emission left behind: the document that was signed, or the
    // reason it is not in good shape.
    private async Task<(string Issue, Guid? InvoiceId, EcfFiscalState? State)> IssueTheFiscalDocumentAsync(
        Sale sale,
        Guid companyId,
        CancellationToken ct)
    {
        var issued = await electronicInvoices.IssueAsync(sale.Id, companyId, ct);

        if (issued is Either<Error, ElectronicInvoiceDto>.Left failure)
            return (failure.Value.Message, null, null);

        var invoice = ((Either<Error, ElectronicInvoiceDto>.Right)issued).Value;

        return (string.Empty, invoice.Id, EcfFiscalStateClassifier.Classify(invoice.Status, invoice.RequiresReview));
    }

    public async Task<Either<Error, SaleDto>> CancelSaleAsync(Guid id, Guid companyId, Guid? userId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, Guid>
            .FromRight(id)
            .Ensure(x => x != Guid.Empty, new Error("id must not be empty."))
            .Ensure(_ => companyId != Guid.Empty, new Error("companyId must not be empty."))
            .FlatMap(async saleId => await unitOfWork.Sales.GetSaleByIdAsync(saleId, ct),
                ex => new Error(ex.Message))
            // The sale of another company is a sale that does not exist for
            // the caller: the answer does not tell what the other tenant
            // has.
            .Ensure(sale => sale is not null && sale.CompanyId == companyId,
                new Error("Sale not found.", ErrorKind.NotFound))
            .FlatMap(async sale =>
            {
                var pending = sale!;

                // The domain has the last word: a sale that is not pending
                // cannot be cancelled.
                pending.Cancel();

                // The sale stays on record: it is the trail of what the
                // cashier closed, and the stock it reserved comes back.
                var products = await TheProductsOfTheTenantAsync(pending, ct);
                RestoreTheReservedStock(pending, products);

                pending.Apply(s =>
                {
                    s.UpdatedAt = DateTime.UtcNow;
                    if (userId.HasValue) s.SetUpdatedBy(userId.Value);
                });

                await unitOfWork.Sales.UpdateSaleAsync(pending, ct);
                await unitOfWork.CommitAsync(ct);
                return pending;
            }, ex => new Error(ex.Message))
            .Map(sale => sale.MapTo<Sale, SaleDto>())
            .Run();
    }

    public async Task<Either<Error, int>> ExpirePendingSalesAsync(TimeSpan lifetime, CancellationToken ct = default)
    {
        return await EitherAsync<Error, TimeSpan>
            .FromRight(lifetime)
            .Ensure(window => window > TimeSpan.Zero,
                new Error("The lifetime of a pending sale must be greater than zero."))
            .FlatMap(async window => await ExpireThePendingSalesAsync(window, ct), ex => new Error(ex.Message))
            .Run();
    }

    public async Task<Either<Error, Unit>> DeleteSaleAsync(Guid id, Guid companyId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, Guid>
            .FromRight(id)
            .Ensure(x => x != Guid.Empty, new Error("id must not be empty."))
            .FlatMap(async saleId => await unitOfWork.Sales.GetSaleByIdAsync(saleId, ct),
                ex => new Error(ex.Message))
            .Ensure(sale => sale is not null, new Error("Sale not found.", ErrorKind.NotFound))
            .FlatMap(async sale =>
            {
                // A document that already left is a trail: the sale cannot be
                // erased. The electronic document that was signed but never
                // sent is not a trail yet, and it goes with the sale.
                if (sale!.ElectronicInvoices.Any(document => HasReachedTheDgii(document.Status)))
                    throw new InvalidOperationException(
                        "The fiscal document of this sale was already sent to the DGII: the sale cannot be deleted.");

                // The paper document took a number of the authorized range the
                // moment the sale was completed: that number is reported.
                if (await unitOfWork.NormalFiscalDocuments.GetBySaleIdAsync(companyId, sale.Id, ct) is not null)
                    throw new InvalidOperationException(
                        "The fiscal document of this sale was already issued: the sale cannot be deleted.");

                var products = await TheProductsOfTheTenantAsync(sale!, ct);
                RestoreTheReservedStock(sale!, products);

                await unitOfWork.Sales.DeleteSaleAsync(id, ct);
                await unitOfWork.CommitAsync(ct);
                return new Unit();
            }, ex => new Error(ex.Message))
            .Run();
    }

    // Only the documents that never reached the DGII can go with the sale:
    // the draft and the signed one. The rest left, and what left is a trail.
    private static bool HasReachedTheDgii(EcfStatus status)
        => status is not (EcfStatus.Draft or EcfStatus.Signed);

    // The pending sales nobody decided expire: the sale stays on record as
    // cancelled and the stock it reserved comes back.
    private async Task<int> ExpireThePendingSalesAsync(TimeSpan lifetime, CancellationToken ct)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var cutoff = now - lifetime;

        var expired = await unitOfWork.Sales.GetPendingOlderThanAsync(cutoff, ct);

        if (expired.Count == 0)
            return 0;

        // The products come in one query: the sweep may close many sales at
        // once and the stock of each one comes back.
        var products = await TheProductsOfTheSweepAsync(expired, ct);

        foreach (var sale in expired)
        {
            sale.Cancel();

            RestoreTheReservedStock(sale, products);

            sale.Apply(s => s.UpdatedAt = now);

            await unitOfWork.Sales.UpdateSaleAsync(sale, ct);
        }

        await unitOfWork.CommitAsync(ct);
        return expired.Count;
    }

    // The stock that a sale reserved comes back to the inventory: it was
    // deducted when the sale was created, and the sale leaves the pending
    // state without becoming a sale.
    private static void RestoreTheReservedStock(Sale sale, IReadOnlyDictionary<Guid, Product> products)
    {
        foreach (var item in sale.Items)
        {
            if (products.TryGetValue(item.ProductId, out var product))
                product.IncrementStock(item.Quantity);
        }
    }

    // The products of the sale, as the tenant of the request sees them.
    private async Task<IReadOnlyDictionary<Guid, Product>> TheProductsOfTheTenantAsync(Sale sale, CancellationToken ct)
    {
        var productIds = sale.Items.Select(item => item.ProductId).ToHashSet();

        if (productIds.Count == 0)
            return new Dictionary<Guid, Product>();

        var products = await unitOfWork.Products.GetProductsByIdsAsync(productIds, ct);
        return products.ToDictionary(product => product.Id);
    }

    // The products of the expired sales. The sweep runs without an HTTP
    // request: the filter of the tenant would hide them, and the stock they
    // reserved would never come back.
    private async Task<IReadOnlyDictionary<Guid, Product>> TheProductsOfTheSweepAsync(IEnumerable<Sale> sales, CancellationToken ct)
    {
        var productIds = sales.SelectMany(sale => sale.Items).Select(item => item.ProductId).ToHashSet();

        if (productIds.Count == 0)
            return new Dictionary<Guid, Product>();

        var products = await unitOfWork.Sales.GetReservedProductsAsync(productIds, ct);
        return products.ToDictionary(product => product.Id);
    }

    // The RNC that issues the sale. It has to be one of the issuers of the
    // tenant: its own RNC or the one of one of its branches. Empty means the
    // tenant itself.
    private async Task<string?> ResolveIssuerTaxIdAsync(string? requestedTaxId, Guid companyId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(requestedTaxId))
            return null;

        return await unitOfWork.CompanyIssuers.ResolveIssuerTaxIdAsync(companyId, requestedTaxId, ct);
    }

    private async Task ResolveReceptorTaxIdAsync(Sale sale, CancellationToken ct)
    {
        if (sale.ClienteId is null)
            return;

        var client = await unitOfWork.Clients.GetClientByIdAsync(sale.ClienteId.Value, ct);

        sale.ReceptorTaxId = client?.TaxId;
    }

    // The user may bring its own code; otherwise the per-company sequence
    // provides one. A user-provided code must be free inside the company.
    private async Task<string> ResolveCodeForCreateAsync(string? requestedCode, Guid companyId, CancellationToken ct)
    {
        var trimmed = requestedCode?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return await unitOfWork.Codes.NextAsync(companyId, CodeEntityType.Sale, ct);

        if (await unitOfWork.Sales.ExistsCodeAsync(companyId, trimmed, null, ct))
            throw new Exception(CodeAlreadyInUseMessage);

        return trimmed;
    }

    // Resolves the code of an existing sale: the user-provided one (trimmed) when it
    // comes with a value, otherwise the code the sale already has. The row being
    // updated is excluded so keeping its own code is not a conflict.
    private async Task<string> ResolveCodeForUpdateAsync(string? requestedCode, string currentCode, Guid companyId, Guid id, CancellationToken ct)
    {
        var trimmed = requestedCode?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return currentCode;

        if (await unitOfWork.Sales.ExistsCodeAsync(companyId, trimmed, id, ct))
            throw new Exception(CodeAlreadyInUseMessage);

        return trimmed;
    }
}
