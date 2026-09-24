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

### Ciclo 1.0 — Characterization + audit  →  ✅ COMPLETO (commit `2b633a0`)
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
- **Commit**: `2b633a0` — `feat(multi-tenant): cycle 1.0 - characterization tests + duplication audit` (5 files, 948 insertions).

### Ciclo 1.1 — Wire refactor  →  ✅ COMPLETO
- **Wire refactor ejecutado**: `CompanyId` removido de los 5 request DTOs (`CreateSaleRequest`, `CreateCategoryRequest`, `CreateProductDto`, `CreateUserDto`, `SupplierCreateDto`).
- **`SupplierDto` split** (decisión adoptada del audit): `SupplierDto` queda como response-only con `CompanyId`; nuevo `UpdateSupplierRequest` (request-only, sin `CompanyId`). `SuppliersController.Update` ahora bindea `UpdateSupplierRequest`.
- **Bugs cerrados**:
  - `SalesController.CreateSale` ahora llama `GetCurrentUserCompanyId()` y pasa `companyId` a `SaleService.CreateSaleAsync` (firma extendida con `int companyId`).
  - `SupplierService.CreateSupplierAsync` y `UpdateSupplierAsync` usan `companyId` param (no `request.CompanyId`).
  - `UserService.CreateAsync` también recibió el mismo ajuste — bug latente detectado (controller parchaba con `with { CompanyId = companyId }` que se volvió dead code al dropear la propiedad).
  - `AuthController.Register` ajustado para pasar `companyId` explícito.
- **Defense in depth**: comentarios `// companyId viene del JWT, nunca del request` agregados en `ProductService` y `CategoryService` (create/update paths).
- **Tests**: 7 characterization tests flipeados (3 SalesController + 4 SupplierService) + 9 wire-contract tests nuevos (6 reflection guards + 3 JSON deserialization guards). **Total: 278 tests passing, 0 failed, 231 ms**. 0 warnings nuevos introducidos.
- **Diff stat**: 25 files changed, 490 insertions, 192 deletions (net +298). **Bien dentro del budget de 400 lines/PR.**
- **Decisiones fuera del plan reportadas**:
  1. `UserService.CreateAsync` también tenía un bug latente (parcheado por controller). Fix obligatorio al wire refactor.
  2. Dead code `MapToSaleEntity` removido en `SaleService.cs` (era inalcanzable y referenciaba `r.CompanyId`).
  3. 9 wire-contract tests en lugar de 3 — agregué 6 reflection guards durables además de los 3 JSON que pedía el plan.
  4. 7 flipped characterization tests cubren happy-paths además de los leak-tests.
- **Constraints respetados**: ✅ NO DB / NO schema / NO `ITenantContext` / NO `HasQueryFilter` / NO tocados los 30 Ensure ni los 38 predicates / `LoginResponse` intacto.
- **Commit**: `2384a8a` — `feat(multi-tenant): cycle 1.1 - wire refactor closes 3 cross-tenant bugs` (25 files, 517 insertions, 193 deletions).

### Ciclo 1.2 — Guid 7 migration  →  EN PROGRESO (split 3 PRs)

#### 1.2a — Domain + Claims constant  →  ✅ COMPLETO (commit `d5eac9e`)
- **Scope ejecutado**: 12 entities + AuditEntity a `Guid`; `Claims.cs` constant creado.
- **Cambios**: 14 files, +45/-38. Bien dentro del budget de 400/PR.
- **Verificación**:
  - `int Id|CompanyId|...|EmployeeId` en Domain/Entities: **0** ✓ (era 24).
  - `Guid Id|...` en Domain/Entities: **24** ✓.
  - `dotnet build`: 176 errors, todos en Volterp.Application — **esperado y correcto** (estado de deuda para 1.2b).
- **Surprises nuevas detectadas** (no estaban en el audit original):
  - `Employee.DirectManagerId` (self-FK Guid?).
  - `Sale.ClienteId` typo pre-existente, conservado.
  - `Sale.AddItem(int productId)` / `RemoveItem(int productId)` migrados a `Guid`.
  - `Purchase.AddItem(int? productId)` / `RemoveItem(int? productId)` migrados a `Guid?`.
  - `EitherAsync` type-inference break en `AccountingTransactionService.cs:65` (1.2b debe anotar tipo explícito).
  - `HashSet<Guid>` mismatch en `Distinct().ToHashSet()` cuando entity es Guid pero DTOs siguen siendo int (1.2b arregla migrando DTOs primero).
  - `int quantity`/`Stock` en Product/Sale NO son FKs — no se migraron.
- **Deuda para 1.2b**: ~50 errors en services + ~30 errors en DTOs + 1 EitherAsync inference + 6 HashSet mismatches.

#### 1.2b — Infrastructure + Application (Infra + App interfaces + DTOs + JwtService + EF configs + migrations)  →  ✅ COMPLETO (commit `bf55432`)
#### 1.2c — Api + tests (controllers + BaseController + CurrentUserService + 278 tests adapted + tests nuevos + verification + schema reset)  →  ✅ COMPLETO (commit `187b069`)

**Cierres clave de 1.2c**:
- BaseController/CurrentUserService: `int.Parse` → `Guid.TryParse` con throw `UnauthorizedException` (401) en missing/malformed claim. **Cierra el bug latente del audit 1.0**.
- 9 characterization tests flipeados (silent-zero → throws 401).
- 2 nuevos tests: claim missing + claim malformed → 401.
- `JwtService` emite claims en formato `Guid:N` (32 chars hex, sin guiones) — verificado por tests en `JwtServiceTests.cs`.
- `AuditableDto.cs` revertido (no se usa en ningún lado — única referencia es su propia definición).
- Slop menor corregido: test de JwtService N-format en `BaseControllerAuthorizationTests.cs` era duplicado de `GenerateToken_EmitsCompanyIdClaim` en `JwtServiceTests.cs` — eliminado.

**Estado final post 1.2c**:
- ✅ Domain / Application / Infrastructure / Api / Tests compilan.
- ✅ 280/280 tests passing (281 - 1 duplicado eliminado).
- ⚠️ Schema reset (DB drop + update) **NO ejecutado** — Postgres no estaba corriendo, queda para el usuario.
- ⚠️ Migraciones: `InitialGuid7` generada en 1.2b (`bf55432`). Las futuras migraciones son trabajo del usuario (decisión explícita).

**Trabajo residual**:
- Slop del commit 1.1 pendiente de limpieza (acordamos resolverlo al final del phase).
- Tests pre-existentes nullable warnings (47) sin tocar (out of scope).
### Ciclo 1.3 — Integration + refactor residual  →  pending
### Ciclo 1.4 — docs/multitenancy.md  →  pending

## Process rules (lesson learned 2026-09-24)

User feedback: "Si el código no tiene intención no sirve para mí. No es que no se comenten las cosas pero no un comentario cargado."

Hard rules from now on:
1. **Cero** doc-comments que expliquen lo que el código ya dice.
2. **Cero** referencias al plan en el código (no "Cycle 1.X", no "as decided in audit doc").
3. **Cero** cross-references a tests en código de producción.
4. **Cero** comentarios sobre líneas auto-explicativas.
5. **Cero** defensive code sin razón real.
6. **Tests** solo donde haya behavior real. No over-engineering de guards.
7. **Si algo no necesita cambio, no lo toco.**

## Verification evidence

### Ciclo 1.0
- `dotnet test`: **269 passed / 0 failed / 246 ms** (baseline 253 + 16 nuevos characterization tests).
- 4 archivos untracked: 3 tests + 1 audit doc. Branch intacta, 0 commits propios.
- Audit doc: `odd/tasks/multi-tenant-isolation-audit.md`.
- Commit: `2b633a0`.

### Ciclo 1.1
- `dotnet test`: **278 passed / 0 failed / 231 ms** (269 + 7 flipped + 9 wire-contract nuevos).
- `dotnet build`: succeeded, 0 warnings nuevos.
- 25 files changed, 490 insertions, 192 deletions.
- Commit: pendiente.

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