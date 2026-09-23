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
        var result = await serviceManager.AccountingTransactions.GetByIdAsync(id, companyId, ct);
        
        return result.Match<ActionResult<AccountingTransactionDto>>(
            error => BadRequest(new ErrorResponse(error.Message)),
            transaction => transaction is null 
                ? NotFound(new { message = "Transaccion contable no encontrada" }) 
                   : Ok(transaction)
        );
    }

    [HttpPost]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<AccountingTransactionDto>> CreateTransaction([FromBody] AccountingTransactionDto request, CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var userId = GetCurrentUserId() ?? 0;
        var result = await serviceManager.AccountingTransactions.CreateAsync(request, companyId, userId, ct);
        return result.Match<ActionResult<AccountingTransactionDto>>(
            error => BadRequest(new ErrorResponse(error.Message)),
            transaction => CreatedAtAction(nameof(GetTransaction), new { id = transaction.Id }, transaction)
        );
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<AccountingTransactionDto>> UpdateTransaction(int id, [FromBody] AccountingTransactionDto request, CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var userId = GetCurrentUserId() ?? 0;
        var result = await serviceManager.AccountingTransactions.UpdateAsync(id, companyId, request, userId, ct);
        return result.Match<ActionResult<AccountingTransactionDto>>(
            error => NotFound(new { message = error.Message }),
            transaction => Ok(transaction)
        );
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<ActionResult> DeleteTransaction(int id, CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var result = await serviceManager.AccountingTransactions.DeleteAsync(id, companyId, ct);
        return result.Match<ActionResult>(
            error => NotFound(new { message = error.Message }),
            _ => NoContent()
        );
    }
}