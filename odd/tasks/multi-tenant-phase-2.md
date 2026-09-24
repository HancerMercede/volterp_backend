# Multi-tenant — Phase 2 (Future work from Phase 1)

ODD task tracker. Phase 1 (5 TDD cycles) closed in commits `2b633a0` → `a34549a` on `refactor/multi-tenant-system`. 283/283 tests passing.

Phase 1 left behind five items that were deferred or noted as "future work". This document tracks them.

## Tasks

- [ ] **Task 1 — Remove 33 `Ensure(companyId != Guid.Empty, ...)` boilerplate**
  - File scope: `Volterp.Application/Services/*.cs` only.
  - Pattern: `.Ensure(_ => companyId != Guid.Empty, new Error("..."))` → delete the Ensure clause.
  - Why now: `BaseController.GetCurrentUserCompanyId()` throws 401 before services run, so the runtime guard is unreachable. `ITenantContext` covers the rest.
  - Hard rule: use `Edit` for each deletion; do not rewrite services. Do not add comments.
  - Verification: `git grep "companyId != Guid.Empty" Volterp.Application` → 0.
  - Risk: if a service was called from somewhere other than a controller (background job, seeder), the Ensure was the only tenant guard. Audit callers first. Search: `git grep -l "service\.\|Service\." --include="*.cs"` for non-controller call sites.

- [ ] **Task 2 — Consolidate `BaseController.GetCurrentUserCompanyId` + `CurrentUserService.GetCompanyId` into a single `HttpTenantContext` consumer**
  - File scope: `Volterp.Api/Controllers/BaseController.cs`, `Volterp.Api/Helpers/CurrentUserService.cs`.
  - Approach: pick one consumer. `HttpTenantContext.CompanyId` is already the source of truth (via `ITenantContext`). `BaseController.GetCurrentUserCompanyId()` could be replaced by reading from an injected `ITenantContext`. `CurrentUserService` could be deleted if no other service uses it (verify with grep).
  - Why now: duplication dates from before `ITenantContext` existed. The 401-throw semantic must stay — either in `BaseController` or in a new wrapper.
  - Hard rule: do not change service signatures. Do not introduce new abstractions.
  - Verification: `git grep "GetCurrentUserCompanyId" Volterp` → 1 (BaseController only). `git grep "CurrentUserService" Volterp` → only the file being deleted and any remaining callers.
  - Risk: `CurrentUserService.IsSuperAdmin` is referenced by `UserService` (per memory #1539). Keep that path intact.

- [ ] **Task 3 — Implement Message Handler `ITenantContext`** — **BLOCKED**
  - Blocker: Volterp has no message broker yet. Background jobs do not exist. No concrete handler to model this against.
  - When unblocked: implement `MessageTenantContext` reading tenant from message envelope (e.g., a `TenantId` header on the queue or message metadata). Register as scoped per handler invocation. Never share across threads.
  - Re-check when first queue/handler is added.

- [ ] **Task 4 — Implement Admin Global `ITenantContext`** — **BLOCKED**
  - Blocker: no admin cross-tenant endpoints exist yet. `BaseController.IsSuperAdminOnly()` is defined but unused (search: `git grep "IsSuperAdminOnly" Volterp`).
  - When unblocked: implement `AdminTenantContext` returning a fixed `Guid` paired with `IgnoreQueryFilters()` in admin repos. Wrap every admin endpoint in `IsSuperAdminOnly()`. Audit annually.
  - Re-check when first admin endpoint is added.

- [ ] **Task 5 — Wire Serilog enricher for `CompanyId`**
  - File scope: `Volterp.Api/` (new middleware) + `Program.cs` (1 line).
  - Pattern: middleware after `UseAuthentication` that pushes `LogContext.PushProperty("CompanyId", tenant.CompanyId)` for the request lifetime.
  - Why now: every log entry during a request should carry the tenant scope so cross-tenant tracing is trivial. Low risk, isolated.
  - Hard rule: do not modify existing middleware. Add a new one.
  - Verification: run integration test with log capture; assert each log entry during an authenticated request has `CompanyId` property.

## Out of scope (deferred explicitly)

- Schema reset (`dotnet ef database drop --force` + `dotnet ef database update`) — user-owned work.
- Reviewing the migrations generated in `bf55432` — user-owned decision.

## Applicable checks per task

- `dotnet test` (all green).
- `dotnet build` (0 errors, no new warnings).
- Greps of regression:
  - `git grep "companyId != Guid.Empty" Volterp.Application` → must drop by exactly the deleted count.
  - `git grep "GetCurrentUserCompanyId" Volterp` → must converge to 1 (BaseController).
  - `git grep "HasQueryFilter" Volterp.Infrastructure` → 3 (unchanged, these are Task 3/1.3 work).

## Delivery

- Work-unit commits: one per task.
- Push, PR, merge: user-owned decision.
- Branch: continue on `refactor/multi-tenant-system` or open a new branch per task — TBD with user before starting.

## Relevant Files

- Plan doc: `odd/tasks/multi-tenant-isolation.md` (Phase 1, completed)
- Audit doc: `odd/tasks/multi-tenant-isolation-audit.md` (Cycle 1.0, historical)
- Operational guide: inside `odd/tasks/multi-tenant-isolation.md` ("Operational guide" section)
- Phase 1 commits: `2b633a0` (1.0), `2384a8a` (1.1), `d9ab96d` (1.1 cleanup), `d5eac9e`/`bf55432`/`187b069` (1.2 a/b/c), `d613727`/`afcdeb6`/`6803319` (1.3), `75d415c`/`a34549a` (docs)