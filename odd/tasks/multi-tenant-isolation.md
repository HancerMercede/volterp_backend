# Multi-tenant isolation — Phase 1 (5 TDD cycles)

## Objective
Refactor del aislamiento multi-tenant en Volterp backend: DB única, aislamiento a nivel de dominio, migración de IDs a `Guid 7`. Branch: `refactor/multi-tenant-system` (basada en `development` @ `62339ed`).

## Problem (estado actual auditado)
- 12 entities con `int Id` (AccountingTransaction, Category, Client, Company, Employee, Product, Purchase, PurchaseItem, Sale, SaleItem, Supplier, User).
- Predicados manuales `c.CompanyId == companyId` repetidos en cada repo.
- `companyId` en request DTOs (cliente-controlled).
- **3 bugs reales de cross-tenant leak**:
  - `SalesController.Create` ignora JWT.
  - `SupplierService.Create` y `SupplierService.Update` usan `request.CompanyId`.
- Duplicación detectada:
  - `GetCurrentUserCompanyId()` en 2 lugares (`BaseController` + `CurrentUserService`).
  - Literal `"companyId"` en 3 lugares.
  - `Ensure(companyId > 0)` boilerplate en ~30 lugares.
- `int.Parse(claim?.Value ?? "0")` falla silenciosa con 0 → debería ser 401.

## Why
El usuario quiere arquitectura limpia donde el **dominio declara membresía** y **EF enforce con `HasQueryFilter`**. Eliminar dependencia oculta en boundaries de ejecución. Cerrar los 3 bugs cross-tenant antes de seguir creciendo el sistema.

## Architectural decisions (cerradas)
- **Domain-centric**: `ITenantScoped` en `Volterp.Domain/Abstractions`; `ITenantContext` (interface) en `Volterp.Application/Abstractions`; impl HTTP en `Volterp.Api/Http/HttpTenantContext.cs`; `HasQueryFilter` en Infrastructure. Domain no depende de EF ni HTTP.
- **Guid 7** (`Guid.CreateVersion7()`): timestamp en primeros bits → locality en B-tree de Postgres → menos page splits que UUIDv4. Disponible en .NET 10 con monotonicidad intra-millisecond. **Formato `N`** (32 chars sin guiones) en JWT claims.
- **Greenfield migration**: schema reset (`DROP SCHEMA public CASCADE; CREATE SCHEMA public;`) + `dotnet ef database update`. Seeders usan Guid 7 con valores fijos (tests deterministas).
- **Derek Comartin — 4 contextos**:
  1. HTTP (implícito vía JWT, throw 401 si falta claim).
  2. Message Handler (explícito en envelope del mensaje).
  3. Admin Global (explícito, provider dedicado para SuperAdmin cross-tenant).
  4. Tests/Seeder (explícito o `NullTenantContext`).
  - Loggear `CompanyId` vía Serilog enricher.
  - Whitelist cerrada de `IgnoreQueryFilters()`.
- **Wire refactor**: `companyId` sale de todos los request DTOs. `AuthController.Login` sigue devolviendo `companyId` en `LoginResponse`.

## Scope (Phase 1 — 5 ciclos TDD)

- [ ] **Ciclo 1.0** — Characterization tests del comportamiento actual + auditoría de duplicación (sin tocar DB ni wire).
- [ ] **Ciclo 1.1** — Wire refactor: `companyId` sale de los 5 request DTOs.
- [ ] **Ciclo 1.2** — Guid 7 migration: `int Id` → `Guid Id` en 12 entities + JWT claim + schema reset.
- [ ] **Ciclo 1.3** — Integration tests de aislamiento + refactor residual de `Ensure(companyId > 0)`.
- [ ] **Ciclo 1.4** — `docs/multitenancy.md` con Fase 0 (4 contextos).

## Constraints
- DB actual es disposable (datos de prueba del usuario) → schema reset OK.
- **TDD estricto**: ciclos red → green → refactor. Tests en mismo PR que implementación.
- Characterization tests primero (capturan comportamiento actual antes de refactorizar).
- No más `companyId` en request DTOs.
- `companyId` se mantiene en `LoginResponse` (lo necesita el cliente para UI).
- Default confirmado: formato `N` para Guid en JWT claims.
- Branch creada desde `development` @ `62339ed` — base limpia.

## Acceptance criteria
- [ ] `HasQueryFilter` reemplaza predicados manuales en repos.
- [ ] Los 3 bugs cross-tenant corregidos con tests de regresión.
- [ ] Cero `companyId` en request DTOs (excepto `LoginResponse`).
- [ ] Cero `Ensure(companyId > 0)` boilerplate.
- [ ] Constante única `Claims.CompanyId` (no más literal `"companyId"`).
- [ ] `BaseController` y `CurrentUserService` consolidados en `HttpTenantContext` + `ICurrentUserService`.
- [ ] Schema reset ejecutado, migraciones regeneradas con Guid 7.
- [ ] Tests passing con strict TDD.
- [ ] `docs/multitenancy.md` describe los 4 contextos.

## Applicable checks (por ciclo)
- `dotnet test` (todos los tests verdes).
- `dotnet build` (sin warnings nuevos).
- `dotnet ef migrations add ...` + `dotnet ef database update` (en 1.2).
- Greps de regresión:
  - `rg "Ensure\(companyId" Volterp.Application Volterp.Api` → debe dar 0.
  - `rg "request\.CompanyId|request\.CompanyID" Volterp.Application/DTOs` → debe dar 0.
  - `rg "\"companyId\""` → debe dar 1 sola ocurrencia en `Claims.cs`.

## Delivery
- ~400 authored changed lines / PR budget (heurística, no cap).
- Work-unit commits: 1 commit por ciclo (red + green + refactor juntos si caben).
- Push, PR y merge: decisión del usuario bajo ordinary repo policy.

## Progress

### Ciclo 1.0 — Characterization + audit  →  ✅ COMPLETO
- **Map verified**: 12 entities con `int Id` confirmados; `Company` no implementa tenant scope; `SaleItem` co-located en `Sale.cs:78-94`.
- **3 bugs cross-tenant confirmados** con `file:line`:
  - `Volterp.Api/Controllers/SalesController.cs:71-79` — `CreateSale` nunca llama `GetCurrentUserCompanyId()`.
  - `Volterp.Application/Services/SupplierService.cs:31-54` + DTO `SupplierCreateDto.cs:24` — `request.CompanyId` se persiste.
  - `Volterp.Application/Services/SupplierService.cs:75` — update path reasigna `s.CompanyId = request.CompanyId`.
- **Duplicación cuantificada** (con counts y locations exactas):
  - 30× `Ensure(_ => companyId > 0, …)` en 10 services.
  - 38× `CompanyId == companyId` predicates manuales (17× en `DashboardRepository` solo).
  - 3× literal `"companyId"` (BaseController, CurrentUserService, JwtService).
  - 41× invocaciones a `GetCurrentUserCompanyId()` en 10 controllers.
  - 5× request DTOs exponiendo `CompanyId` (CreateSaleRequest, CreateCategoryRequest, CreateProductDto, CreateUserDto, SupplierCreateDto).
- **Tests**: 16 nuevos characterization tests, **todos passing**. Baseline: 253 → Final: 269 (+16), 0 failed, 246 ms.
  - `Volterp.Tests/Controllers/SalesControllerCreateTenantLeakTests.cs` (3 tests).
  - `Volterp.Tests/Services/SupplierServiceTenantLeakTests.cs` (4 tests).
  - `Volterp.Tests/Controllers/CurrentUserCompanyIdCharacterizationTests.cs` (9 tests).
- **Audit doc**: `odd/tasks/multi-tenant-isolation-audit.md` (233 líneas).
- **Surprises no en el plan**:
  - `int.Parse` (throw) vs `int.TryParse` (no throw) — inconsistente entre `GetCurrentUserCompanyId()` y `GetCurrentUserId()` en `BaseController`. Se surface en 1.2 cuando Guid 7 introduzca claims no-numéricos.
  - `CurrentUserService` implementado pero bypassed por todos los controllers. Path recomendado: eliminarlo + consolidar en `HttpTenantContext`.
  - `AuditEntity.CreatedBy`/`UpdatedBy` son `int?` — también afectados por Guid 7 migration (no estaban en el plan original).
  - `SaleItem` co-located con `Sale` (no archivo separado).
  - `DashboardRepository` con 17 predicados manuales → candidate para strangler-fig en 1.3.
- **Seam identificado para 1.3**: agregar `public partial class Program {};` a `Program.cs` destraba `WebApplicationFactory<Program>` para integration tests reales.
- **Commit**: pendiente de decisión del usuario (ver bloque de cierre).

### Ciclo 1.1 — Wire refactor  →  pending
### Ciclo 1.2 — Guid 7 migration  →  pending
### Ciclo 1.3 — Integration + refactor residual  →  pending
### Ciclo 1.4 — docs/multitenancy.md  →  pending

## Verification evidence

### Ciclo 1.0
- `dotnet test`: **269 passed / 0 failed / 246 ms** (baseline 253 + 16 nuevos characterization tests).
- 4 archivos untracked: 3 tests + 1 audit doc. Branch intacta, 0 commits propios.
- Audit doc: `odd/tasks/multi-tenant-isolation-audit.md`.

## Next step
Ejecutar Ciclo 1.0: characterization tests + auditoría de duplicación. Sin tocar DB ni wire.

## Relevant Files (paths relativos al repo)
- Entities: `Volterp.Domain/Entities/{AccountingTransaction,Category,Client,Company,Employee,Product,Purchase,PurchaseItem,Sale,SaleItem,Supplier,User}.cs`
- Controllers: `Volterp.Api/Controllers/{SalesController,BaseController,AuthController}.cs`
- Services: `Volterp.Application/Services/SupplierService.cs` (bug confirmado)
- Helpers: `Volterp.Api/Helpers/{CurrentUserService,Constants}.cs`
- JwtService: `Volterp.Infrastructure/Services/JwtService.cs:22`
- DTOs: `Volterp.Application/DTOs/*` (5 con `CompanyId` a quitar)
- A crear: `Volterp.Domain/Abstractions/ITenantScoped.cs`, `Volterp.Domain/ValueObjects/CompanyId.cs`, `Volterp.Application/Abstractions/ITenantContext.cs`, `Volterp.Api/Http/HttpTenantContext.cs`, `Volterp.Application/Common/Claims.cs`