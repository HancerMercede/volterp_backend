using EitherWay;
using Volterp.Application.DTOs;
using Volterp.Application.DTOs.EmployeeDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Helpers;

namespace Volterp.Application.Interfaces;

public interface IEmployeeService
{
    Task<PagedResult<EmployeeDto>> GetAllEmployeesAsync(Guid companyId, int pageNumber, int pageSize, CancellationToken ct = default);
    Task<Either<Error, EmployeeDto?>> GetEmployeeByIdAsync(Guid id, Guid companyId, CancellationToken ct = default);
    Task<Either<Error, EmployeeDto>> CreateEmployeeAsync(CreateEmployeeDto request, Guid companyId, Guid userId, CancellationToken ct = default);
    Task<Either<Error, EmployeeDto>> UpdateEmployeeAsync(Guid id, Guid companyId, UpdateEmployeeDto request, Guid userId, CancellationToken ct = default);
    Task<Either<Error, Unit>> DeleteEmployeeAsync(Guid id, Guid companyId, CancellationToken ct = default);
}
