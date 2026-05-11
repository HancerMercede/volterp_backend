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
public class CompaniesController(IServiceManager serviceManager, ILogger<CompaniesController> logger):BaseController
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<CompanyDto>>> GetAllCompanies([FromQuery] PaginationParameters parameters, CancellationToken ct = default)
    {
        if (!IsAdmin())
            return Forbid();
        try
        {
            return Ok(await serviceManager.Companies.GetAllCompaniesAsync(parameters.PageNumber,parameters.PageSize, ct));
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return NotFound(new ErrorResponse($"There are no companies."));
        }
       
    }
    
    [HttpGet("{id}", Name = "GetCompany")]
    public async Task<ActionResult<CompanyDto>> GetCompany(int id, CancellationToken ct = default)
    {
        if (!IsAdmin())
            return Forbid();
        try
        {
            return Ok(await serviceManager.Companies.GetCompanyByIdAsync(id, ct));
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return NotFound(new ErrorResponse($"Company with {id}  not found."));
        }
       
    }

    [HttpPost]
    public async Task<ActionResult<CompanyDto>> CreateCompany([FromBody] CreateCompanyDto createCompanyDto,
        CancellationToken ct = default)
    {
        if (!IsAdmin())
            return Forbid();
        try
        {
            var company = await serviceManager.Companies.AddCompanyAsync(createCompanyDto, ct);
            return Created("GetCompany", company);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return BadRequest(new  ErrorResponse("Company creation failed.", e.Message));
        }

    }

    [HttpPut("{id}")]
    public async Task<ActionResult> UpdateCompany(int id, [FromBody] UpdateCompanyDto updateCompanyDto,
        CancellationToken ct = default)
    {
        if(!IsAdmin())
            return Forbid();

        try
        {
            var company = await serviceManager.Companies.UpdateCompanyAsync(id, updateCompanyDto, ct);
            return Ok(company);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return BadRequest(new  ErrorResponse("Company modification failed.", e.Message));
        }
       
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteCompany(int id, CancellationToken ct = default)
    {
        if(!IsAdmin())
            return Forbid();
        try
        {
            await serviceManager.Companies.DeleteCompanyAsync(id, ct);
            return NoContent();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return  BadRequest(new  ErrorResponse("Company deletion failed.", e.Message));
        }
  
    }
}