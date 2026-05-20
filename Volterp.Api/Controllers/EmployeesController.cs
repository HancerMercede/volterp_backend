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
public class EmployeesController(IServiceManager serviceManager) : BaseController
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<EmployeeDto>>> GetAllEmployees(
        [FromQuery] PaginationParameters pagination,
        CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var result = await serviceManager
            .Employees
            .GetAllEmployeesAsync(companyId, pagination.PageNumber, pagination.PageSize, ct);

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<EmployeeDto>> GetEmployee(int id, CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var employee = await serviceManager
            .Employees
            .GetEmployeeByIdAsync(id, companyId, ct);

        if (employee is null)
            return NotFound(new { message = "Empleado no encontrado" });

        return Ok(employee);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<EmployeeDto>> CreateEmployee([FromBody] EmployeeDto request, CancellationToken ct = default)
    {
        try
        {
            var companyId = GetCurrentUserCompanyId();
            var userId = GetCurrentUserId() ?? 0;
            var employee = await serviceManager
                .Employees
                .CreateEmployeeAsync(request, companyId, userId, ct);

            return CreatedAtAction(nameof(GetEmployee), new { id = employee.Id }, employee);
        }
        catch (Exception e)
        {
            return BadRequest(new ErrorResponse("Could not create employee", e.Message));
        }
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<EmployeeDto>> UpdateEmployee(int id, [FromBody] EmployeeDto request, CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();

        try
        {
            var userId = GetCurrentUserId() ?? 0;
            var employee = await serviceManager
                .Employees
                .UpdateEmployeeAsync(id, companyId, request, userId, ct);
            return Ok(employee);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<ActionResult> DeleteEmployee(int id, CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();

        try
        {
            await serviceManager.Employees.DeleteEmployeeAsync(id, companyId, ct);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}