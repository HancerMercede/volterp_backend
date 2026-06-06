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
public class AccountingTransactionsController(IServiceManager serviceManager) : BaseController
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<AccountingTransactionDto>>> GetAllTransactions(
        [FromQuery] PaginationParameters pagination,
        CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var result = await serviceManager
            .AccountingTransactions
            .GetAllByCompanyAsync(companyId, pagination.PageNumber, pagination.PageSize, ct);

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<AccountingTransactionDto>> GetTransaction(int id, CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var transaction = await serviceManager
            .AccountingTransactions
            .GetByIdAsync(id, companyId, ct);

        if (transaction is null)
            return NotFound(new { message = "Transaccion contable no encontrada" });

        return Ok(transaction);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<AccountingTransactionDto>> CreateTransaction([FromBody] AccountingTransactionDto request, CancellationToken ct = default)
    {
        try
        {
            var companyId = GetCurrentUserCompanyId();
            var userId = GetCurrentUserId() ?? 0;
            var transaction = await serviceManager
                .AccountingTransactions
                .CreateAsync(request, companyId, userId, ct);

            return CreatedAtAction(nameof(GetTransaction), new { id = transaction.Id }, transaction);
        }
        catch (Exception e)
        {
            return BadRequest(new ErrorResponse("Could not create accounting transaction", e.Message));
        }
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<AccountingTransactionDto>> UpdateTransaction(int id, [FromBody] AccountingTransactionDto request, CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();

        try
        {
            var userId = GetCurrentUserId() ?? 0;
            var transaction = await serviceManager
                .AccountingTransactions
                .UpdateAsync(id, companyId, request, userId, ct);
            return Ok(transaction);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<ActionResult> DeleteTransaction(int id, CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();

        try
        {
            await serviceManager.AccountingTransactions.DeleteAsync(id, companyId, ct);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}