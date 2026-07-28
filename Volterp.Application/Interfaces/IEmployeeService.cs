using EitherWay;
using Volterp.Application.DTOs;
using Volterp.Application.DTOs.EmployeeDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Helpers;

namespace Volterp.Application.Interfaces;

public interface IEmployeeService
{
    Task<PagedResult<EmployeeDto>> GetAllEmployeesAsync(int companyId, int pageNumber, int pageSize, CancellationToken ct = default);
    Task<Either<Error, EmployeeDto?>> GetEmployeeByIdAsync(int id, int companyId, CancellationToken ct = default);
    Task<Either<Error, EmployeeDto>> CreateEmployeeAsync(CreateEmployeeDto request, int companyId, int userId, CancellationToken ct = default);
    Task<Either<Error, EmployeeDto>> UpdateEmployeeAsync(int id, int companyId, UpdateEmployeeDto request, int userId, CancellationToken ct = default);
    Task<Either<Error, Unit>> DeleteEmployeeAsync(int id, int companyId, CancellationToken ct = default);
}