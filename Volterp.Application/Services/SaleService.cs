using EitherWay;
using Volterp.Application.DTOs.NormalFiscalDocumentDtos;
using Volterp.Application.DTOs.SaleDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
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

                // The code is resolved before any stock movement: a rejected code
                // must not leave the inventory half-updated.
                sale.Code = await ResolveCodeForCreateAsync(req.Code, companyId, ct);

                // Batch query: single SELECT for all products (avoids N+1)
                var productIds = sale.Items.Select(i => i.ProductId).ToHashSet();
                var products = await unitOfWork.Products.GetProductsByIdsAsync(productIds, ct);
                var productMap = products.ToDictionary(p => p.Id);

                // Validate and deduct stock for each item
                // NOTE: Stock is deducted at creation time to reserve inventory.
                // The double-deduction behavior (at creation AND completion) is preserved.
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
            .Ensure(sale => sale is not null, new Error("Sale not found."))
            .Ensure(_ => request.Status != SaleStatus.Completed,
                new Error("A sale can only be completed through the completion operation."))
            .Ensure(sale => sale is not null && sale.Status != SaleStatus.Completed,
                new Error("A completed sale cannot be updated."))
            .FlatMap(async sale =>
            {
                // The code is resolved before any stock movement: a rejected code
                // must not leave the inventory half-updated.
                var code = await ResolveCodeForUpdateAsync(request.Code, sale.Code, companyId, sale.Id, ct);

                // Collect all product IDs (old items + new items)
                var allProductIds = sale.Items.Select(i => i.ProductId)
                    .Concat(request.Items.Select(i => i.ProductId))
                    .ToHashSet();

                // Batch query: single SELECT for all products involved
                var products = await unitOfWork.Products.GetProductsByIdsAsync(allProductIds, ct);
                var productMap = products.ToDictionary(p => p.Id);

                // Build product name lookup for error messages
                var newItemNames = request.Items.ToDictionary(i => i.ProductId, i => i.ProductName);

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

    public async Task<Either<Error, SaleDto>> CompleteSaleAsync(Guid id, Guid companyId, Guid? userId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, Guid>
            .FromRight(id)
            .Ensure(x => x != Guid.Empty, new Error("id must not be empty."))
            .FlatMap(async saleId => await unitOfWork.Sales.GetSaleByIdAsync(saleId, ct),
                ex => new Error(ex.Message))
            .Ensure(sale => sale is not null, new Error("Sale not found."))
            .FlatMap(async sale =>
            {
                sale.Complete();
                // Validate and deduct stock for each item
                var productIds = sale.Items.Select(i => i.ProductId).ToHashSet();
                if (productIds.Count > 0)
                {
                    var products = await unitOfWork.Products.GetProductsByIdsAsync(productIds, ct);
                    var productMap = products.ToDictionary(p => p.Id);

                    foreach (var item in sale.Items)
                    {
                        if (!productMap.TryGetValue(item.ProductId, out var product))
                            continue;

                        product.DeductStock(item.Quantity);
                    }
                }

                sale.Apply(s =>
                {
                    s.UpdatedAt = DateTime.UtcNow;
                    if (userId.HasValue) s.SetUpdatedBy(userId.Value);
                });

                await unitOfWork.Sales.UpdateSaleAsync(sale, ct);

                var now = _timeProvider.GetUtcNow().UtcDateTime;

                if (!CompanyEmissionModeService.UsesElectronicEmission(sale.Company, now))
                {
                    // The paper document takes its number from the range the
                    // DGII authorized for the normal documents, and the
                    // number, the document and the completion of the sale land
                    // together. A document that could not be issued leaves the
                    // sale incomplete: the caller gets the reason.
                    var issued = await _normalDocuments.IssueAsync(sale.Id, companyId, userId, ct);

                    issued.Match<NormalFiscalDocumentDto>(
                        error => throw new InvalidOperationException(error.Message),
                        document => document);

                    return sale;
                }

                await unitOfWork.CommitAsync(ct);

                // The e-CF of the sale is queued here and the durable queue
                // sends it in the background. The sale is a commercial fact:
                // the fiscal document of the client cannot undo it, and the
                // document that could not be signed stays on record for a
                // human.
                await electronicInvoices.IssueAsync(sale.Id, companyId, ct);

                return sale;
            }, ex => new Error(ex.Message))
            .Map(sale => sale.MapTo<Sale, SaleDto>())
            .Run();
    }

    public async Task<Either<Error, Unit>> DeleteSaleAsync(Guid id, Guid companyId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, Guid>
            .FromRight(id)
            .Ensure(x => x != Guid.Empty, new Error("id must not be empty."))
            .FlatMap(async saleId => await unitOfWork.Sales.GetSaleByIdAsync(saleId, ct),
                ex => new Error(ex.Message))
            .Ensure(sale => sale is not null, new Error("Sale not found."))
            .FlatMap(async sale =>
            {
                // Only restore stock if there are items with product IDs
                var productIds = sale.Items.Select(i => i.ProductId).ToHashSet();
                if (productIds.Count > 0)
                {
                    var products = await unitOfWork.Products.GetProductsByIdsAsync(productIds, ct);
                    var productMap = products.ToDictionary(p => p.Id);

                    foreach (var item in sale.Items)
                    {
                        if (productMap.TryGetValue(item.ProductId, out var product))
                            product.IncrementStock(item.Quantity);
                    }
                }

                await unitOfWork.Sales.DeleteSaleAsync(id, ct);
                await unitOfWork.CommitAsync(ct);
                return new Unit();
            }, ex => new Error(ex.Message))
            .Run();
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
