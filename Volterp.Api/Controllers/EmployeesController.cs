using Volterp.Application.DTOs.EmployeeDtos;

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
        var result = await serviceManager.Employees.GetEmployeeByIdAsync(id, companyId, ct);
        return result.Match<ActionResult<EmployeeDto>>(
            error => BadRequest(new ErrorResponse(error.Message)),
            employee => employee is null ? NotFound(new { message = "Empleado no encontrado" }) : Ok(employee)
        );
    }

    [HttpPost]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<EmployeeDto>> CreateEmployee([FromBody] CreateEmployeeDto request, CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var userId = GetCurrentUserId() ?? 0;
        var result = await serviceManager.Employees.CreateEmployeeAsync(request, companyId, userId, ct);
        return result.Match<ActionResult<EmployeeDto>>(
            error => BadRequest(new ErrorResponse(error.Message)),
            employee => CreatedAtAction(nameof(GetEmployee), new { id = employee.Id }, employee)
        );
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<EmployeeDto>> UpdateEmployee(int id, [FromBody] UpdateEmployeeDto request, CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var userId = GetCurrentUserId() ?? 0;
        var result = await serviceManager.Employees.UpdateEmployeeAsync(id, companyId, request, userId, ct);
        return result.Match<ActionResult<EmployeeDto>>(
            error => NotFound(new { message = error.Message }),
            employee => Ok(employee)
        );
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<ActionResult> DeleteEmployee(int id, CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var result = await serviceManager.Employees.DeleteEmployeeAsync(id, companyId, ct);
        return result.Match<ActionResult>(
            error => NotFound(new { message = error.Message }),
            _ => NoContent()
        );
    }
}