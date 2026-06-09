using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volterp.Api.Helpers;
using Volterp.Application.DTOs;
using Volterp.Application.DTOs.ClientDtos;
using Volterp.Application.Interfaces;

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
        var companyId = GetCurrentUserCompanyId();
        var result = await serviceManager
            .Clients
            .GetAllClientsAsync(companyId, pagination.PageNumber, pagination.PageSize, ct);
        
        
        return Ok(result);
    }
    
    [HttpGet("{id}")]
    public async Task<ActionResult<ClientDto>> GetClient(int id, CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var client = await serviceManager
            .Clients
            .GetClientByIdAsync(id, companyId, ct);
        
       
            
        return client.Match<ActionResult<ClientDto>>(
            error => NotFound(error.Message),
            result=> Ok(result));
    }
    
    [HttpPost]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<ClientDto>> CreateClient([FromBody] CreateClientDto request, CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var client = await serviceManager
            .Clients
            .CreateClientAsync(request, companyId, ct);

        return client.Match<ActionResult<ClientDto>>(
            error => BadRequest(error.Message),
            result => CreatedAtAction(nameof(GetClient), 
                new { id = result.Id }, result));
    }
    
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<ClientDto>> UpdateClient(int id, [FromBody] UpdateClientDto request, CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        
        try
        {
            var client = await serviceManager.Clients.UpdateClientAsync(id, companyId, request, ct);
            return Ok(client);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
    
    [HttpDelete("{id}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<ActionResult> DeleteClient(int id, CancellationToken ct = default)
    {
        if(!IsAdmin())
            return Forbid();
        
        var companyId = GetCurrentUserCompanyId();
        
        try
        {
            await serviceManager.Clients.DeleteClientAsync(id, companyId, ct);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
