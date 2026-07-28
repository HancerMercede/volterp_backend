using EitherWay;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volterp.Api.Helpers;
using Volterp.Application.DTOs;
using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;

namespace Volterp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PurchasesController(IServiceManager serviceManager) : BaseController
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<PurchaseDto>>> GetAllPurchases(
        [FromQuery] PaginationParameters pagination,
        CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var result = await serviceManager
            .Purchases
            .GetAllPurchasesAsync(companyId, pagination.PageNumber, pagination.PageSize, ct);

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<PurchaseDto>> GetPurchase(int id, CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var result = await serviceManager.Purchases.GetPurchaseByIdAsync(id, companyId, ct);
        return result.Match<ActionResult<PurchaseDto>>(
            error => BadRequest(new ErrorResponse(error.Message)),
            purchase => purchase is null ? NotFound(new { message = "Compra no encontrada" }) : Ok(purchase)
        );
    }

    [HttpPost]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<PurchaseDto>> CreatePurchase([FromBody] PurchaseDto request, CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var userId = GetCurrentUserId();
        var result = await serviceManager.Purchases.CreatePurchaseAsync(request, companyId, userId, ct);
        return result.Match<ActionResult<PurchaseDto>>(
            error => BadRequest(new ErrorResponse(error.Message)),
            purchase => CreatedAtAction(nameof(GetPurchase), new { id = purchase.Id }, purchase)
        );
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<PurchaseDto>> UpdatePurchase(int id, [FromBody] PurchaseDto request, CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var userId = GetCurrentUserId();
        var result = await serviceManager.Purchases.UpdatePurchaseAsync(id, companyId, request, userId, ct);
        return result.Match<ActionResult<PurchaseDto>>(
            error => NotFound(new { message = error.Message }),
            purchase => Ok(purchase)
        );
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<ActionResult> DeletePurchase(int id, CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var result = await serviceManager.Purchases.DeletePurchaseAsync(id, companyId, ct);
        return result.Match<ActionResult>(
            error => NotFound(new { message = error.Message }),
            _ => NoContent()
        );
    }
}