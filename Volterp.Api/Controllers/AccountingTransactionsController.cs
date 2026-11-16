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
        EnsureTenant();
        var result = await serviceManager
            .AccountingTransactions
            .GetAllAsync(pagination.PageNumber, pagination.PageSize, ct);

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<AccountingTransactionDto>> GetTransaction(Guid id, CancellationToken ct = default)
    {
        EnsureTenant();
        var result = await serviceManager.AccountingTransactions.GetByIdAsync(id, ct);

        return result.Match<ActionResult<AccountingTransactionDto>>(
            error => BadRequest(new ErrorResponse(error.Message)),
            transaction => transaction is null
                ? NotFound(new ErrorResponse("Transaccion contable no encontrada"))
                   : Ok(transaction)
        );
    }

    [HttpPost]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<AccountingTransactionDto>> CreateTransaction([FromBody] AccountingTransactionDto request, CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var userId = GetCurrentUserId() ?? Guid.Empty;
        var result = await serviceManager.AccountingTransactions.CreateAsync(request, companyId, userId, ct);
        return result.Match<ActionResult<AccountingTransactionDto>>(
            error => BadRequest(new ErrorResponse(error.Message)),
            transaction => CreatedAtAction(nameof(GetTransaction), new { id = transaction.Id }, transaction)
        );
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<AccountingTransactionDto>> UpdateTransaction(Guid id, [FromBody] AccountingTransactionDto request, CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var userId = GetCurrentUserId() ?? Guid.Empty;
        var result = await serviceManager.AccountingTransactions.UpdateAsync(id, companyId, request, userId, ct);
        return result.Match<ActionResult<AccountingTransactionDto>>(
            error => NotFound(new ErrorResponse(error.Message)),
            transaction => Ok(transaction)
        );
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<ActionResult> DeleteTransaction(Guid id, CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var result = await serviceManager.AccountingTransactions.DeleteAsync(id, companyId, ct);
        return result.Match<ActionResult>(
            error => NotFound(new ErrorResponse(error.Message)),
            _ => NoContent()
        );
    }
}