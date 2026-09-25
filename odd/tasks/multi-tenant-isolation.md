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
- **Tenant contexts** (3 aplicables; el contexto Message Handler del blog de Derek Comartin NO aplica — Volterp es ERP síncrono, no event-driven, sin message broker):
  1. HTTP (implícito vía JWT, throw 401 si falta claim).
  2. Admin Global (explícito, provider dedicado para SuperAdmin cross-tenant).
  3. Tests/Seeder (explícito o `NullTenantContext`).
  - Loggear `CompanyId` vía Serilog enricher.
  - Whitelist cerrada de `IgnoreQueryFilters()`.
- **Wire refactor**: `companyId` sale de todos los request DTOs. `AuthController.Login` sigue devolviendo `companyId` en `LoginResponse`.

## Scope (Phase 1 — 5 ciclos TDD)

- [x] **Ciclo 1.0** — Characterization tests + auditoría de duplicación. Commit `2b633a0`.
- [x] **Ciclo 1.1** — Wire refactor: `companyId` sale de los 5 request DTOs. 3 bugs cross-tenant cerrados. Commit `2384a8a` + cleanup `d9ab96d`.
- [x] **Ciclo 1.2** — Guid 7 migration: 12 entities + JWT claim. Split a/b/c: `d5eac9e` / `bf55432` / `187b069`. Schema reset diferido al usuario.
- [x] **Ciclo 1.3** — Integration tests + `HasQueryFilter` vía `ITenantContext`. Commits `d613727` / `afcdeb6` / `6803319` / `75d415c`.
- [x] **Ciclo 1.4** — Operational guide incorporado a este plan (no `docs/multitenancy.md` separado). Commit `a34549a`.

## Constraints
- DB actual es disposable (datos de prueba del usuario) → schema reset OK.
- **TDD estricto**: ciclos red → green → refactor. Tests en mismo PR que implementación.
- Characterization tests primero (capturan comportamiento actual antes de refactorizar).
- No más `companyId` en request DTOs.
- `companyId` se mantiene en `LoginResponse` (lo necesita el cliente para UI).
- Default confirmado: formato `N` para Guid en JWT claims.
- Branch creada desde `development` @ `62339ed` — base limpia.

## Acceptance criteria
- [x] `HasQueryFilter` reemplaza predicados manuales en repos.
- [x] Los 3 bugs cross-tenant corregidos con tests de regresión.
- [x] Cero `companyId` en request DTOs (excepto `LoginResponse`).
- [ ] Cero `Ensure(companyId > 0)` boilerplate. — *Diferido a 1.4+ (no era requisito del 1.3 quirúrgico).*
- [x] Constante única `Claims.CompanyId` (no más literal `"companyId"`).
- [ ] `BaseController` y `CurrentUserService` consolidados en `HttpTenantContext` + `ICurrentUserService`. — *Diferido a 1.4+ (no era requisito del 1.3 quirúrgico).*
- [ ] Schema reset ejecutado, migraciones regeneradas con Guid 7. — *Trabajo del usuario (Postgres apagado en sesión 1.2c).*
- [x] Tests passing con strict TDD.
- [x] Operational guide describe los contextos (HTTP ✅, Admin Global ❌, Tests/Seeder ✅; Message Handler descartado porque el ERP es síncrono). — *Ciclo 1.4, incorporado en sección "Operational guide" abajo.*

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
### Ciclo 1.3 — HasQueryFilter + integration tests  →  ✅ COMPLETO (commits `d613727` + `afcdeb6` + `6803319`)

**Approach aprobado** (siguiendo plan original del blog de codeopinion.com — Derek Comartin):
- `ITenantContext` interface en `Volterp.Application/Abstractions/`
- `HttpTenantContext` impl en `Volterp.Api/Http/` (lee `IHttpContextAccessor` + JWT claim `companyId`)
- `NullTenantContext` impl en `Volterp.Application/Abstractions/` (design-time/seeders)
- `VolterpDbContext` inyecta `ITenantContext` opcional + aplica `HasQueryFilter` vía reflection en `OnModelCreating` (NO editado 13 Configurations individuales — reflection centralizado es el approach estándar del blog)
- `Company` se salta (es el tenant, no tenant-scoped)
- `SaleItem`/`PurchaseItem` con filter navigation-based (`e.Sale.CompanyId`, `e.Purchase.CompanyId`)

**Hard rules respetadas**:
- ✅ NO services ni controllers existentes tocados.
- ✅ NO consolidé `BaseController`/`CurrentUserService`.
- ✅ NO eliminé `CurrentUserService`.
- ✅ NO toqué `AuditableDto.cs`.
- ✅ `ApiAssemblyMarker` (NO partial Program) — decisión del usuario en sesión 1.3.

**Predicates eliminados** (grep empírico):
- 33 predicates `CompanyId == companyId` en 10 repos (NO 38 como decía el spec original — discrepancia corregida).
- `DashboardRepository.cs`: 17 predicates (incluye 2 navigation-based en `si.Sale.CompanyId`).
- Otros repos: 16 predicates distribuidos en 9 archivos.

**Bugfix crítico** (commit `6803319`):
- Versión inicial (commit `afcdeb6`) capturaba `tenant` como closure-local: `var tenant = _tenantContext.CompanyId; .HasQueryFilter(e => tenant == null || e.CompanyId == tenant)`.
- EF Core's parameter-extraction visitor inlinea el closure-local como SQL constant al model-build time → filter se lockeaba al primer valor visto (típicamente `null` durante `EnsureCreated` fuera de HTTP) → todas las queries unfiltered.
- Fix: referenciar `_tenantContext.CompanyId` directamente en la lambda (field access del DbContext) → EF Core parameteriza por query.
- Sin este fix, los 3 integration tests nuevos fallaban: cross-tenant reads pasaban, NoCompanyClaim retornaba 200 en vez de 401.

**Tests**:
- 280 → **283 passing** (3 nuevos integration tests reales con HTTP pipeline + SQLite in-memory).
- `Volterp.Tests/Integration/SupplierTenantIsolationTests.cs` (3 tests: cross-tenant read blocked, cross-tenant update blocked, NoCompanyClaim → 401).
- Tests usan `IJwtService.GenerateToken` real + `WebApplicationFactory<ApiAssemblyMarker>` + `EnsureCreated` en SQLite.

**Commits**:
- `d613727` — `feat(multi-tenant): cycle 1.3 step 1 - add ApiAssemblyMarker for WebApplicationFactory`
- `afcdeb6` — `feat(multi-tenant): cycle 1.3 - apply HasQueryFilter via ITenantContext + remove 33 manual predicates` (con bug del closure-local)
- `6803319` — `feat(multi-tenant): integration tests for HasQueryFilter via real HTTP pipeline` (incluye fix del closure-local bug)

**Trabajo del usuario** (no del agente):
- Schema reset (`dotnet ef database drop --force` + `dotnet ef database update`).
- Si las migrations generadas en 1.2b no le sirven, revertir commit `bf55432`.

### Ciclo 1.4 — Operational guide → ✅ COMPLETO (incorporado en este plan)

En vez de crear `docs/multitenancy.md` separado (se solapaba con este plan), los pasos prácticos, el patrón de integration tests y los gotchas se incorporaron como la sección **Operational guide** abajo.

## Operational guide

### TL;DR

- New tenant-scoped entity? Add `Guid CompanyId`. `VolterpDbContext.OnModelCreating` picks it up via reflection and applies `HasQueryFilter` automatically.
- New endpoint? Use `BaseController.GetCurrentUserCompanyId()`. It throws `UnauthorizedException` (401) if the JWT lacks the `companyId` claim.
- Cross-tenant admin? Wrap in `IsSuperAdminOnly()` (already in `BaseController`) and pair with `IgnoreQueryFilters()` deliberately.
- Background job? N/A — Volterp is sync ERP, no async jobs.

### How the layers fit

| Layer | Role | File |
|---|---|---|
| `ITenantContext` | Read-only access to current `CompanyId` | `Volterp.Application/Abstractions/ITenantContext.cs` |
| `HttpTenantContext` | Production impl: reads JWT `companyId` claim (Guid in `N` format) | `Volterp.Api/Http/HttpTenantContext.cs` |
| `NullTenantContext` | No-op for design-time (migrations, seeders) | `Volterp.Application/Abstractions/NullTenantContext.cs` |
| `VolterpDbContext.OnModelCreating` | Applies `HasQueryFilter` per entity via reflection; `Company` excluded, `SaleItem`/`PurchaseItem` use navigation filters | `Volterp.Infrastructure/Data/VolterpDbContext.cs` |
| `BaseController.GetCurrentUserCompanyId` | Throws 401 when JWT lacks the claim | `Volterp.Api/Controllers/BaseController.cs` |

### The 3 contexts (status)

| # | Context | Status | Notes |
|---|---|---|---|
| 1 | HTTP (JWT, throw 401 si falta claim) | ✅ implemented | `HttpTenantContext` is the registered default. `BaseController` throws before services run. |
| 2 | Admin Global (cross-tenant explícito) | ❌ not implemented | Implement `ITenantContext` returning a fixed `Guid` paired with `IgnoreQueryFilters()` in admin repos. Wrap in `IsSuperAdminOnly()`. Blocked: no admin endpoints exist yet. |
| 3 | Tests / Seeder (explícito o `NullTenantContext`) | ✅ implemented | `NullTenantContext` is the design-time default. Integration tests use `WebApplicationFactory<ApiAssemblyMarker>` + custom tenant context per test. |

### Adding a new tenant-scoped entity

1. Add `Guid CompanyId` to the entity.
2. Add an EF Configuration in `Volterp.Infrastructure/Data/Configurations/` if one does not exist.
3. Done — `VolterpDbContext.OnModelCreating` reflection picks up the `CompanyId` property and applies the filter automatically.
4. Add an integration test that seeds two tenants and verifies cross-tenant blocking for the new entity.

### Integration tests pattern

```csharp
public class SupplierTenantIsolationTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public SupplierTenantIsolationTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VolterpDbContext>();
        db.Database.EnsureCreated();
    }

    [Fact]
    public async Task CrossTenantRead_IsBlocked()
    {
        // 1. Seed a supplier for CompanyA and another for CompanyB.
        // 2. Mint a JWT for CompanyB via IJwtService.
        // 3. GET /api/suppliers with CompanyB's JWT.
        // 4. Assert: only CompanyB's supplier appears.
    }
}
```

The fixture (`Volterp.Tests/Integration/ApiFactory.cs`) wires up `WebApplicationFactory<ApiAssemblyMarker>` with SQLite in-memory, gates `db.Database.Migrate()` on `!app.Environment.IsEnvironment("Testing")`, and registers `HttpTenantContext` per request.

### Observability (not yet wired)

- Log `CompanyId` via Serilog `LogContext.PushProperty("CompanyId", tenant.CompanyId)` in a middleware that sits after authentication.
- Every log entry produced during a request should carry the tenant scope so cross-tenant tracing is trivial.

### Gotchas

| Pitfall | Why it bites | Fix |
|---|---|---|
| Capturing `_tenantContext.CompanyId` as a closure-local inside `HasQueryFilter` | EF Core inlines closure-locals as SQL constants at model-build time. The filter locks to the first value seen (typically `null` during `EnsureCreated` outside HTTP). All queries become unfiltered. | Reference the DbContext field directly in the lambda: `e => _tenantContext.CompanyId == null \|\| EF.Property<Guid>(e, "CompanyId") == _tenantContext.CompanyId`. EF Core parameterizes field access per query. |
| Forgetting to gate `db.Database.Migrate()` in tests | The startup runs `Migrate()` against the real connection string. In integration tests with SQLite in-memory, this throws because migrations are Postgres-specific. | `Program.cs` already gates with `if (!app.Environment.IsEnvironment("Testing")) db.Database.Migrate();`. Do not remove this gate. |
| Reading `CompanyId` from `IHttpContextAccessor` in a background job | `HttpContext` is null outside an HTTP request. The implicit tenant context is wrong for background work. | Use a Message-Handler-flavored `ITenantContext` reading from the message envelope. Not implemented yet — see context 2 above. |
| Returning `Guid.Empty` when the claim is missing | Silent data corruption, not 401. | Always go through `BaseController.GetCurrentUserCompanyId()` so missing claims produce 401. |
| Calling `IgnoreQueryFilters()` outside admin paths | Bypasses tenant isolation. | If you need it, you are doing admin work. Wrap in `IsSuperAdminOnly()`. |

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

## Phase 2 (future work)

Phase 1 cerrada. Esta fase cubre el trabajo futuro opcional listado en `## Next step` previo. Scope confirmado en sesión 2026-09-24: 4 tareas; Message Handler queda explícitamente descartado (Volterp es ERP síncrono, no event-driven).

### Estado de las tareas

| # | Tarea | Estado | Bloqueador |
|---|---|---|---|
| 1 | Eliminar 33 `Ensure(companyId != Guid.Empty, ...)` en services | Pendiente | — |
| 2 | Consolidar `BaseController.GetCurrentUserCompanyId` → consumer de `ITenantContext` | Pendiente | — |
| 3 | Implementar **Admin Global** `ITenantContext` (cross-tenant) | **BLOQUEADO** | No hay endpoints admin (ver `IsSuperAdminOnly` call sites: 4, todos en `UsersController`) |
| 4 | Wirear Serilog enricher para `CompanyId` | Pendiente | — |
| ~~5~~ | ~~Message Handler `ITenantContext`~~ | **DESCARTADO** | ERP síncrono, sin message broker |

### Task 1 — Remove 33 `Ensure(companyId != Guid.Empty, ...)` boilerplate
- **File scope**: `Volterp.Application/Services/*.cs` (10 archivos).
- **Patrón exacto**: `.Ensure(_ => companyId != Guid.Empty, new Error("..."))` → eliminar línea + ajustar chain.
- **Variantes detectadas** (todas a eliminar):
  - Forma estándar `_` (28×): `AccountingTransactionService`, `PurchaseService`, `SaleService`, `UserService`, `CategoryService`, `ClientService`, `EmployeeService`, `ProductService`, `SupplierService` (mayoría).
  - Alias `c` (1×): `DashboardService.cs:14` — `.Ensure(c => c != Guid.Empty, ...)`.
  - Multi-line (1×): `SupplierService.cs:42-43` con indentación rota.
  - Strings de error inconsistentes (28 "companyId", 2 "company id", 2 "The companyId", 1 "the companyId") — todos a eliminar.
- **Razón**: `BaseController.GetCurrentUserCompanyId()` lanza 401 antes de que el service corra → el guard es unreachable. `ITenantContext` cubre el resto.
- **Hard rules**: usar `Edit` (no `Write`), no agregar comments, no tocar firmas de service.
- **Verificación**:
  - `git grep "companyId != Guid.Empty" Volterp.Application` → 0.
  - `git grep "Guid.Empty" Volterp.Application/Services` → 0 (excluyendo falsos positivos).
  - `dotnet test` → 283 passing (sin cambios).
  - `dotnet build` → 0 errors, 0 warnings nuevos.
- **TDD**: dead-code elimination, no cycle red→green→refactor. Caracterización previa = los 283 tests existentes ya cubren el behavior. Si siguen verdes post-eliminación, el cambio es seguro.
- **Commit**: `chore(multi-tenant): remove 33 Ensure(companyId != Guid.Empty) boilerplate`.

### Task 2 — Consolidate `BaseController.GetCurrentUserCompanyId` → `ITenantContext` consumer
- **File scope**: `Volterp.Api/Controllers/BaseController.cs`.
- **Approach**: reemplazar el body de `GetCurrentUserCompanyId()` para leer de `ITenantContext.CompanyId` en vez de leer el claim directo. Mantener firma pública exacta (`Guid`, throws 401).
- **Razón**: `HttpTenantContext.CompanyId` ya es la fuente de verdad (registrado en DI). `BaseController.GetCurrentUserCompanyId()` duplica la lectura del claim.
- **NO** se hace en este task:
  - Eliminar `CurrentUserService` (lo usa `UserService.IsSuperAdmin()`).
  - Cambiar firmas de service.
  - Tocar los 47 call sites de `GetCurrentUserCompanyId()`.
- **Hard rules**: `Edit` solamente, no agregar comments, no romper los 16 tests de `BaseControllerAuthorizationTests` + `CurrentUserCompanyIdCharacterizationTests`.
- **Verificación**:
  - `dotnet test` → 283 passing (sin cambios).
  - 16 tests de `BaseControllerAuthorizationTests` + `CurrentUserCompanyIdCharacterizationTests` siguen verdes sin tocarlos.
- **TDD**: los tests existentes son la red. Si siguen verdes, el refactor preserva el behavior.
- **Commit**: `refactor(multi-tenant): consolidate BaseController.GetCurrentUserCompanyId via ITenantContext`.

### Task 3 — Admin Global `ITenantContext` (BLOQUEADO)
- **Bloqueador**: no hay endpoints admin cross-tenant. `IsSuperAdminOnly()` está definido en `BaseController.cs:32-33` pero solo se usa en `UsersController` (4 call sites), y todos son para autorizar acciones de users — no para queries cross-tenant.
- **Cuando se desbloquee**: implementar `AdminTenantContext` con Guid fijo + `IgnoreQueryFilters()` en admin repos. Wrap en `IsSuperAdminOnly()`.
- **Re-check** cuando aparezca el primer endpoint admin cross-tenant.

### Task 4 — Wirear Serilog enricher para `CompanyId`
- **File scope**: `Volterp.Api/` (middleware nuevo) + `Program.cs` (1 línea de registration).
- **Approach**: middleware después de `UseAuthentication` que haga `LogContext.PushProperty("CompanyId", tenant.CompanyId)` por el lifetime del request.
- **Razón**: cada log entry durante un request debería llevar el tenant scope para cross-tenant tracing trivial.
- **Hard rules**: no modificar middleware existente, agregar uno nuevo.
- **Verificación**: integration test con log capture; assert cada entry durante request autenticado tiene `CompanyId` property.
- **Riesgo**: bajo, aislado.

### Constraints vigentes (Phase 2)
- Los 7 hard rules del doc (líneas 288-299): cero doc-comments, cero refs al plan en código, cero defensive code, cero over-engineering de tests.
- TDD: dead-code elimination se verifica con los tests existentes; refactor preserva behavior.
- Branch: continuar en `refactor/multi-tenant-system` hasta merge (decisión del usuario).
- Commit messages: conventional commits sin AI attribution.
- Delivery: ~400 líneas/PR heurística. Task 1 = 33 deletions (bien dentro del budget).

### Out of scope (deferred explícitamente)
- Schema reset (`dotnet ef database drop --force` + `dotnet ef database update`) — user-owned.
- Revisar migrations de `bf55432` — user-owned decision.

### Task 5 — Remove dead `companyId` parameter (Tier 1 only) 🆕
- **File scope**: 9 interfaces de repo + 9 archivos de repo + reads en services + 11 controllers (reads) + tests que pasan companyId a reads.
- **Tier 1 (reads)** — `companyId` se remueve como parámetro en:
  - 22 métodos de repo (todos los `I{Sale,Purchase,Client,Supplier,Employee,Category,Product,User,Accounting}Repository` que aún lo reciben).
  - 6 métodos de `DashboardRepository` (parámetro dead desde Cycle 1.3).
  - 6 Ensure defensivos en CategoryService/ProductService (belt-and-suspenders inertes post-HasQueryFilter).
  - Reads en services (`GetAllX`, `GetById`) — son dead forwards al repo.
- **Tier 2 (writes) — DIFERIDO explícitamente**: requiere decisiones arquitectónicas adicionales que necesitan input del usuario:
  - Si removemos `companyId` parámetro de CreateAsync y el service lee de `ITenantContext`, ¿quién valida 401 cuando falta claim? El controller ya no llamaría `GetCurrentUserCompanyId()`. Opciones: (a) controller helper `EnsureTenant()` para validar claim antes de invocar service, (b) ASP.NET authorization filter, (c) service tira `UnauthorizedException` directamente. Esto necesita decisión antes de implementar.
  - También requiere inyectar `ITenantContext` en 9 service constructors + actualizar `ServiceManager` + actualizar tests de CreateAsync.
  - Estimado: ~350 líneas adicionales.
- **Tier 3 (NO tocar, legítimo)**:
  - `ICompanyRepository` (3 métodos: PK de la entidad, no filtro de tenant).
  - `ICompanyService.UpdateCompanyAsync`.
  - `IJwtService.GenerateToken` (companyId es claim value, no filtro).
- **Razón**: post-HasQueryFilter + post-eliminación de 33 predicados manuales (Cycle 1.3) + post-eliminación de 33 Ensure (Phase 2 Task 1), el `companyId` parámetro es 100% redundante para reads.
- **Hard rules**:
  - Usar `Edit` (no `Write`).
  - NO tocar métodos CreateAsync (siguen recibiendo `companyId`).
  - NO tocar DTO comments (esos son para CreateAsync).
  - Reescribir `ProductServiceTests.cs:20-44` para validar aislamiento via query filter en lugar de via Ensure (porque el Ensure se elimina).
- **Verificación**:
  - `git grep "companyId" Volterp.Infrastructure/Repositories/` → solo en CompanyRepository (Tier 3).
  - `dotnet test` → 283+ passing (los tests que pasan `companyId` a reads deben actualizarse para no pasarlo).
  - `dotnet build` → 0 errors, 0 nuevos warnings.
- **Riesgo**: ~30 archivos tocados, mecânico.
- **Commits esperados**: 1 commit cohesivo.

### Progress
- [x] Task 1 — Ensure elimination (commit `cc7b030`)
- [x] Task 2 — BaseController consolidation (commit `7ebc82e`)
- [ ] Task 3 — Admin Global ITenantContext (BLOCKED — sin endpoints admin)
- [ ] Task 4 — Serilog enricher para CompanyId
- [ ] Task 5 — Remove dead `companyId` parameter

## Next step
- **Task 1 (Ensure elimination)**: arrancar ahora. Decisión técnica clara, dead code, verificado por tests existentes.

## Relevant Files (paths relativos al repo)
- Entities: `Volterp.Domain/Entities/{AccountingTransaction,Category,Client,Company,Employee,Product,Purchase,PurchaseItem,Sale,SaleItem,Supplier,User}.cs`
- Tenant abstraction: `Volterp.Application/Abstractions/{ITenantContext,NullTenantContext}.cs`, `Volterp.Api/Http/HttpTenantContext.cs`, `Volterp.Application/Common/Claims.cs`
- DbContext: `Volterp.Infrastructure/Data/VolterpDbContext.cs` (HasQueryFilter via reflection)
- EF Configurations: `Volterp.Infrastructure/Data/Configurations/*.cs` (13 archivos)
- Repositories (sin predicates manuales post-1.3): `Volterp.Infrastructure/Repositories/*.cs`
- Controllers: `Volterp.Api/Controllers/{BaseController,SalesController,SuppliersController,...}.cs`
- Integration tests: `Volterp.Tests/Integration/{ApiFactory,JwtTestHelper,SupplierTenantIsolationTests}.cs`