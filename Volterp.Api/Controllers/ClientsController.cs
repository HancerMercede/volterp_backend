using Volterp.Application.DTOs.ClientDtos;

namespace Volterp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ClientsController(IServiceManager serviceManager) : BaseController
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<ClientDto>>> GetAllClients(
        [FromQuery] PaginationParameters pagination,
        CancellationToken ct = default)
    {
        EnsureTenant();
        var result = await serviceManager
            .Clients
            .GetAllClientsAsync(pagination.PageNumber, pagination.PageSize, ct);

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ClientDto>> GetClient(Guid id, CancellationToken ct = default)
    {
        EnsureTenant();
        var client = await serviceManager
            .Clients
            .GetClientByIdAsync(id, ct);

        return client.Match<ActionResult<ClientDto>>(
            error => NotFound(new ErrorResponse(error.Message)),
            result => Ok(result));
    }

    [HttpPost]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<ClientDto>> CreateClient([FromBody] CreateClientDto request, CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var userId = GetCurrentUserId();
        var client = await serviceManager
            .Clients
            .CreateClientAsync(request, companyId, userId, ct);

        return client.Match<ActionResult<ClientDto>>(
            error => BadRequest(new ErrorResponse(error.Message)),
            result => CreatedAtAction(nameof(GetClient), new { id = result.Id }, result));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<ClientDto>> UpdateClient(Guid id, [FromBody] UpdateClientDto request, CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var userId = GetCurrentUserId();
        var client = await serviceManager.Clients.UpdateClientAsync(id, companyId, request, userId, ct);

        return client.Match<ActionResult<ClientDto>>(
            error => BadRequest(new ErrorResponse(error.Message)),
            result => Ok(result));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<ActionResult> DeleteClient(Guid id, CancellationToken ct = default)
    {
        if (!IsAdmin())
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("You do not have permission to perform this action."));

        var companyId = GetCurrentUserCompanyId();
        var result = await serviceManager.Clients.DeleteClientAsync(id, companyId, ct);

        return result.Match<ActionResult>(
            error => NotFound(new ErrorResponse(error.Message)),
            _ => NoContent());
    }
}