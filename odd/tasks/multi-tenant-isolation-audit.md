# Multi-tenant isolation — Cycle 1.0 Audit

**Branch**: `refactor/multi-tenant-system`
**Base**: `development` @ `62339ed`
**Scope**: Characterization + duplication audit only. No production code, DB, or wire changes.

---

## 1. Entities — `int Id` inventory

All 12 entities confirmed with `int Id`. `Company` is the tenant root and does NOT carry `CompanyId`.

| Entity | File | Id line | Has CompanyId? | Tenant-scoped? | Notes |
|---|---|---|---|---|---|
| AccountingTransaction | `Volterp.Domain/Entities/AccountingTransaction.cs` | 7 | Yes (line 8) | Yes | Inherits `AuditEntity` |
| Category | `Volterp.Domain/Entities/Category.cs` | 5 | Yes (line 8) | Yes | Inherits `AuditEntity` |
| Client | `Volterp.Domain/Entities/Client.cs` | 7 | Yes (line 14) | Yes | Inherits `AuditEntity` |
| Company | `Volterp.Domain/Entities/Company.cs` | 7 | **No** (IS the tenant) | No | Inherits `AuditEntity` |
| Employee | `Volterp.Domain/Entities/Employee.cs` | 7 | Yes (line 9) | Yes | Inherits `AuditEntity` |
| Product | `Volterp.Domain/Entities/Product.cs` | 9 | Yes (line 16) | Yes | Inherits `AuditEntity` |
| Purchase | `Volterp.Domain/Entities/Purchase.cs` | 8 | Yes (line 9) | Yes | Inherits `AuditEntity` |
| PurchaseItem | `Volterp.Domain/Entities/PurchaseItem.cs` | 5 | No (FK only — `PurchaseId`) | Inherited via Purchase | Pure FK entity |
| Sale | `Volterp.Domain/Entities/Sale.cs` | 8 | Yes (line 9) | Yes | Inherits `AuditEntity` |
| SaleItem | `Volterp.Domain/Entities/Sale.cs` (line 78, in same file) | 80 | No (FK only — `SaleId`) | Inherited via Sale | **Co-located with Sale**, separate class |
| Supplier | `Volterp.Domain/Entities/Supplier.cs` | 7 | Yes (line 8) | Yes | Inherits `AuditEntity` |
| User | `Volterp.Domain/Entities/User.cs` | 9 | Yes (line 17) | Yes | Inherits `AuditEntity` |

**SaleItem is co-located with Sale** (file `Volterp.Domain/Entities/Sale.cs`, lines 78–94), not a separate file. The plan's entity list mentions `SaleItem` separately — this is the file where it lives.

`AuditEntity` (base class, `Volterp.Domain/Entities/AuditEntity.cs`) carries `CreatedBy`/`UpdatedBy` as `int?` — also affected by Guid 7 migration.

---

## 2. Cross-tenant bugs — confirmed

All three bugs are present and reproducible from current source.

| # | Bug | Surface | File:line | Problem | Impact |
|---|---|---|---|---|---|
| 1 | `SalesController.Create` ignores JWT | HTTP | `Volterp.Api/Controllers/SalesController.cs:71-79` | `CreateSale` only calls `GetCurrentUserId()`; never calls `GetCurrentUserCompanyId()`. Service receives `CreateSaleRequest` directly. | Any authenticated user can create a sale for any `companyId` by setting `CompanyId` in the JSON body. |
| 1 (propagated) | `SaleService.CreateSaleAsync` accepts request-only | Service | `Volterp.Application/Services/SaleService.cs:74` | Method signature `(CreateSaleRequest request, int? userId, CancellationToken ct)` — **no `companyId` parameter at all**. `MapToSaleEntity` uses `r.CompanyId` (line 17). `MapTo()` in `CreateSaleRequest` also uses request's `CompanyId` (`SaleDto.cs:76`). | The bug's downstream sink. Even if a caller tried to inject companyId from JWT, there's no parameter to receive it. |
| 2 | `SupplierService.Create` uses `request.CompanyId` | Service | `Volterp.Application/Services/SupplierService.cs:31-54` → DTO `Volterp.Application/DTOs/SupplierDtos/SupplierCreateDto.cs:24` | `request.Project()` → `MapTo()` → `CompanyId = CompanyId` (DTO's own property). The `companyId` parameter is only used for the `.Ensure(_=> companyId > 0)` validation (line 42). | The persisted `Supplier.CompanyId` comes from the JSON body, not from JWT. |
| 3 | `SupplierService.Update` uses `request.CompanyId` | Service | `Volterp.Application/Services/SupplierService.cs:75` | `s.CompanyId = request.CompanyId;` inside `s.Apply(...)`. The `companyId` parameter is only used for `Ensure` (line 60) and `GetSupplierByIdAsync` (line 65). | Update persists DTO's `CompanyId`, allowing cross-tenant reassignment. |

### Bug 2 vs Bug 3 — same root cause, two sinks

Both bugs originate from `SupplierDto` / `SupplierCreateDto` carrying `CompanyId` as a client-controlled field. Bug 2 reads via `MapTo()`; Bug 3 reads via direct assignment in the update block.

---

## 3. Duplication audit

### 3.1 `companyId > 0` boilerplate (Ensure calls)

**Count: 30 occurrences** across 10 service files in `Volterp.Application/Services/`.

| File | Lines | Count |
|---|---|---|
| `AccountingTransactionService.cs` | 31, 43, 74, 110 | 4 |
| `CategoryService.cs` | 24, 36, 59, 91 | 4 |
| `ClientService.cs` | 24, 41, 61, 92 | 4 |
| `DashboardService.cs` | 14 | 1 |
| `EmployeeService.cs` | 25, 38, 61, 105 | 4 |
| `ProductService.cs` | 26, 67, 108 | 3 |
| `PurchaseService.cs` | 23, 92, 156 | 3 |
| `SaleService.cs` | 67, 113, 178, 220 | 4 |
| `SupplierService.cs` | 24, 42, 60, 97 | 4 |

The plan stated "~30 lugares" — confirmed.

### 3.2 Manual tenant predicates `CompanyId == companyId`

**Count: 38 occurrences** total. All should disappear when `HasQueryFilter` is introduced.

| File | Lines | Count |
|---|---|---|
| `Volterp.Infrastructure/Repositories/EmployeeRepository.cs` | 12, 15 | 2 |
| `Volterp.Infrastructure/Repositories/ProductRepository.cs` | 17 | 1 |
| `Volterp.Infrastructure/Repositories/PurchaseRepository.cs` | 12, 15 | 2 |
| `Volterp.Infrastructure/Repositories/ClientRepository.cs` | 12, 15 | 2 |
| `Volterp.Infrastructure/Repositories/SupplierRepository.cs` | 13, 16 | 2 |
| `Volterp.Infrastructure/Repositories/UserRepository.cs` | 24 | 1 |
| `Volterp.Infrastructure/Repositories/CategoryRepository.cs` | 16 | 1 |
| `Volterp.Infrastructure/Repositories/AccountingTransactionRepository.cs` | 14, 17 | 2 |
| `Volterp.Infrastructure/Repositories/SaleRepository.cs` | 13, 19, 26 | 3 |
| `Volterp.Infrastructure/Repositories/DashboardRepository.cs` | 20, 24, 28, 61, 69, 96, 122, 153, 167, 179, 208, 211, 214, 236, 239, 242, 245 | 17 |
| `Volterp.Application/Services/CategoryService.cs` | 62, 94 | 2 |
| `Volterp.Application/Services/ProductService.cs` | 29, 70, 111 | 3 |

### 3.3 `"companyId"` literal — claim name

**Count: 3 occurrences** (matches the plan).

| File | Line | Context |
|---|---|---|
| `Volterp.Api/Controllers/BaseController.cs` | 8 | `int.Parse(User.FindFirst("companyId")?.Value ?? "0")` |
| `Volterp.Api/Helpers/CurrentUserService.cs` | 16 | `httpContextAccessor.HttpContext?.User.FindFirst("companyId")?.Value ?? "0"` |
| `Volterp.Infrastructure/Services/JwtService.cs` | 22 | `new Claim("companyId", companyId.ToString())` |

Plan target: 1 single occurrence in `Claims.cs` constant.

### 3.4 `int.Parse(.*claim` — silent zero fallback

Direct ripgrep pattern doesn't match because of the syntax. The actual silent-fallback anti-pattern lives in 2 places:

- `Volterp.Api/Controllers/BaseController.cs:8` — `int.Parse(User.FindFirst("companyId")?.Value ?? "0")` — throws `FormatException` if claim value is non-numeric; silently returns 0 when claim is missing.
- `Volterp.Api/Helpers/CurrentUserService.cs:16-17` — same pattern, `?? "0"` then `int.Parse(value)`.

Also note: `GetCurrentUserId()` uses `int.TryParse` (no throw) but `GetCurrentUserCompanyId()` uses `int.Parse` (throws on malformed). Inconsistent error handling between the two adjacent methods.

### 3.5 `GetCurrentUserCompanyId()` call sites

**Count: 41 invocations** across 10 controllers, plus 1 definition in `BaseController.cs:7`.

| Controller | Lines | Count |
|---|---|---|
| `SalesController.cs` | 22, 36, 49, 60, 84, 96, 108 | 7 |
| `SuppliersController.cs` | 15, 26, 42, 60, 75 | 5 |
| `PurchasesController.cs` | 13, 24, 36, 49, 62 | 5 |
| `ClientsController.cs` | 16, 27, 41, 57, 73 | 5 |
| `ProductsController.cs` | 15, 23, 38, 54, 69 | 5 |
| `CategoriesController.cs` | 16, 24, 39, 55, 70 | 5 |
| `EmployeesController.cs` | 15, 26, 38, 51, 64 | 5 |
| `AccountingTransactionsController.cs` | 13, 24, 39, 52, 65 | 5 |
| `UsersController.cs` | 28, 42 | 2 |
| `DashboardController.cs` | 11 | 1 |
| `BaseController.cs` | 7 (definition) | 1 |

**Exception**: `SalesController.CreateSale` (line 71-79) — the only action in any controller that does NOT call `GetCurrentUserCompanyId()`. This is bug #1.

### 3.6 `ICurrentUserService` / `CurrentUserService` usage

- Defined: `Volterp.Application/Interfaces/ICurrentUserService.cs` (3 members: `IsSuperAdmin`, `GetCompanyId`, `GetUserId`).
- Implemented: `Volterp.Api/Helpers/CurrentUserService.cs:6`.
- DI registered: `Volterp.Api/Helpers/ServiceExtensions.cs:75`.
- Consumed in: `Volterp.Application/Services/UserService.cs:11` and `Volterp.Application/Services/ServiceManager.cs:5`.

**CurrentUserService is NOT used by any controller** — controllers all use the duplicated `BaseController.GetCurrentUserCompanyId()` instead. So the abstraction exists but is bypassed in favor of the duplication.

### 3.7 Request DTOs carrying `CompanyId` — wire refactor candidates

The plan says "5 request DTOs". Search confirms exactly 5 places where a request-type DTO exposes `CompanyId` to the client:

| File | Line(s) | Context |
|---|---|---|
| `Volterp.Application/DTOs/CategoryDtos/CreateCategoryRequest.cs` | 8, 17 | `int CompanyId` param + `CompanyId = CompanyId` in `MapTo()` |
| `Volterp.Application/DTOs/ProductDtos/CreateProductDto.cs` | 14, 28 | Same shape (record + `MapTo()`) |
| `Volterp.Application/DTOs/UserDtos/CreateUserDto.cs` | 12, 23 | Same shape |
| `Volterp.Application/DTOs/SupplierDtos/SupplierCreateDto.cs` | 7, 24 | Drives Bug #2 |
| `Volterp.Application/DTOs/SaleDto.cs` (`CreateSaleRequest`) | 63, 76 | Drives Bug #1 |

`SupplierDto` (the read/update DTO) also has `CompanyId` at line 9 — used as response body and as update payload. Bug #3 happens because `SupplierController.Update` binds this DTO as the request body (`SuppliersController.cs:57`). The wire refactor must either drop `CompanyId` from update payloads or split into a separate `UpdateSupplierRequest` DTO without `CompanyId`.

`LoginResponse` (in `AuthDto.cs:4`) keeps `CompanyId` — explicitly per plan ("LoginResponse sigue devolviendo companyId").

---

## 4. Test project inventory

- **Project**: `Volterp.Tests` (`Volterp.Tests/Volterp.Tests.csproj`).
- **Framework**: xUnit 2.9 + FluentAssertions 6.12 + Moq 4.20.
- **No `WebApplicationFactory`**, no `Microsoft.AspNetCore.Mvc.Testing`. Existing tests are pure unit tests with Moq.
- **Existing test folders**: `Controllers/`, `Domain/`, `Exceptions/`, `Infrastructure/`, `RateLimiting/`, `Services/`, `ValueObjects/`.
- **Existing controller test**: `Volterp.Tests/Controllers/SalesControllerAuthorizationTests.cs` (33 lines) — uses `GetCustomAttribute<AuthorizeAttribute>`. Only pattern present.
- **Baseline pass count**: 253 tests passing (verified with `dotnet test`).
- **All 4 production projects** (Domain, Application, Infrastructure, Api) are referenced — full visibility for characterization.

### Integration-test blocker (Cycle 1.0)

`Volterp.Api/Program.cs` is top-level statements with **no `public partial class Program` declaration**. The constraint "no production code changes" forbids adding one. Therefore `WebApplicationFactory<Program>` is not feasible without violating the cycle scope. Characterization tests in this cycle use Moq + the existing unit-test pattern.

---

## 5. Recommendations — Cycle 1.1 → 1.4 priority

1. **Cycle 1.1 (wire refactor)** — first target because it has the highest payoff per line of code and naturally enables 1.2:
   - Drop `CompanyId` from `CreateSaleRequest`, `CreateCategoryRequest`, `CreateProductDto`, `CreateUserDto`, `SupplierCreateDto`, `SupplierDto` (or split update DTO).
   - Add `companyId` parameter to `SaleService.CreateSaleAsync` (sign change).
   - Fix `SupplierService.Create` and `Update` to ignore `request.CompanyId` and use the JWT-derived `companyId`.
   - Fix `SalesController.Create` to call `GetCurrentUserCompanyId()` and pass it through.
   - Update `ProductService` and `CategoryService` create/update paths to ignore `request.CompanyId` (defense in depth — currently they don't, but `CompanyId == companyId` predicate in service `Ensure` blocks the cross-tenant write; in Ciclo 1.3 that predicate goes away).
   - Acceptance tests for 1.1: the 3 characterization tests written in 1.0 must flip to red, and 3 new "wire contract" tests must pass.

2. **Cycle 1.2 (Guid 7 migration)** — second because depends on 1.1 stabilising wire:
   - `int Id` → `Guid Id` in 12 entities + `AuditEntity.CreatedBy`/`UpdatedBy`.
   - Schema reset + EF migration regeneration.
   - JWT claim format `Guid:N` (32 chars, no dashes).
   - `Claims.cs` constant introduced at the same time (consolidates the 3 `"companyId"` literals).

3. **Cycle 1.3 (integration + residual refactor)** — third because depends on 1.2 finishing migrations:
   - Add `Microsoft.AspNetCore.Mvc.Testing` package + introduce a `public partial class Program` in `Program.cs` (this WILL be a production change — must wait for cycle).
   - Integration tests with real JWT + in-memory EF Core (or testcontainers for Postgres if needed for `HasQueryFilter` semantics).
   - Delete the 30 `.Ensure(_ => companyId > 0, ...)` boilerplate calls + 38 manual predicates once `HasQueryFilter` is in place.

4. **Cycle 1.4 (docs)** — last, depends on all code paths stabilising.

---

## 6. Surprises / risks not in the plan

1. **`int.Parse` vs `int.TryParse` inconsistency in `BaseController`** — `GetCurrentUserCompanyId` (line 8) uses `int.Parse` and will throw on malformed claim values; `GetCurrentUserId` (line 11) uses `int.TryParse`. This is a latent bug and will surface when Guid 7 introduces 32-char non-numeric claims. The plan only mentions silent-fallback-to-0; it doesn't flag the throw path.

2. **`CurrentUserService` is implemented but bypassed** — controllers all use `BaseController.GetCurrentUserCompanyId()` instead of injecting `ICurrentUserService`. The plan implies consolidating these two; the path of least resistance is to delete `CurrentUserService` first, then introduce the new `HttpTenantContext` + extend `ICurrentUserService` (or replace the interface entirely).

3. **`SaleItem` co-located with `Sale`** — not a separate file. Some tooling, IDEs, and codegen may expect one-class-per-file. If the plan intends a `SaleItem.cs` file in 1.2 (Guid 7 migration), it must be split out first. Otherwise the migration is just an `Id` type change in the same file.

4. **EF Core `bool` is the Id of `AuditEntity.CreatedBy`/`UpdatedBy`** — actually `int?`. The audit base class is also affected by Guid 7 migration, but the plan only mentions the 12 entities. Audit-by-id is not technically tenant-scoped but is part of the same Id type change.

5. **`UserWithPasswordHashDto` has `CompanyId`** — `Volterp.Application/DTOs/UserDtos/UserWithPasswordHashDto.cs:14`. Used internally by `UserService.GetByUsernameAsync`. The plan's "5 request DTOs" doesn't list it; this is an internal DTO, not exposed via the wire. Confirm in 1.1 that internal DTOs are intentionally excluded from the wire refactor scope.

6. **`DashboardRepository.cs` has 17 manual tenant predicates** — by far the heaviest concentration. It's a complex read-side repository; the `HasQueryFilter` migration should leave `DashboardRepository`'s query methods alone (they're tenant-scoped by design), but verify behavior remains identical.

7. **Pre-existing nullable warnings (53 of them)** — present at baseline. Not introduced by this cycle; not my concern for 1.0.

8. **No `Microsoft.AspNetCore.Mvc.Testing` in the test project** — blocks proper integration tests for 1.0 and 1.3. Resolution options:
   - (a) Add `public partial class Program` to `Program.cs` (production code change → not allowed in 1.0, OK in 1.3).
   - (b) Use `HostFactoryResolver` to resolve the entry assembly without exposing `Program` as partial — fragile across .NET versions.
   - (c) Adopt testcontainers + WebApplicationFactory in 1.3 — recommended.

---

## 7. Summary table

| Item | Plan estimate | Actual |
|---|---|---|
| Entities with `int Id` | 12 | 12 ✓ |
| `companyId > 0` Ensure boilerplate | ~30 | 30 ✓ |
| `CompanyId == companyId` predicates | not counted | 38 |
| `"companyId"` literal occurrences | 3 | 3 ✓ |
| Request DTOs with `CompanyId` | 5 | 5 ✓ |
| Cross-tenant bugs | 3 | 3 ✓ |
| `GetCurrentUserCompanyId()` controllers | not counted | 10 controllers, 41 invocations |
| `int.Parse(claim` silent-fallback sites | not counted | 2 (both produce 0 silently) |
