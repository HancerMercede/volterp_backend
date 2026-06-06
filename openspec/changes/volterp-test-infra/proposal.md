# Proposal: volterp-test-infra

## Intent

Establish a dual-project test infrastructure (Volterp.Tests for .NET backend, erp-mvp tests for React frontend) that guarantees system stability as features are added. The primary immediate goal: **tests that detect Bug #1 (SaleService stock not decremented) and Bug #2 (PurchaseService stock not incremented) before the bugs are fixed**.

## Scope

### In Scope
- **Backend**: Create `Volterp.Tests` xUnit project with Moq + FluentAssertions
  - Unit tests for all Application services (SaleService, PurchaseService, ProductService, UserService)
  - Integration tests for Controllers via WebApplicationFactory
  - Repository pattern tests with in-memory or test container DB
  - Coverage target: ≥80% on SaleService, PurchaseService, ProductService
- **Frontend**: Extend existing Vitest suite in erp-mvp
  - Store tests (ventaStore, productoStore, compraStore)
  - API service tests (fetchWithAuth, saleService, productService)
  - Utility tests (jwt helpers)
  - Coverage target: ≥80% on stores and API services

### Out of Scope
- E2E tests (Playwright/Cypress — future phase)
- Performance/load tests
- Visual regression tests
- Frontend component tests beyond stores/API utils

## Capabilities

### New Capabilities
- `backend-test-infrastructure`: xUnit + Moq test project with unit, integration, and repository tests
- `frontend-test-infrastructure`: Extended Vitest coverage for stores and API services
- `stock-decrement-verification`: Test that proves SaleService.CompleteSaleAsync() decrements product stock
- `stock-increment-verification`: Test that proves PurchaseService.CreatePurchaseAsync() increments product stock

### Modified Capabilities
- None (greenfield infrastructure)

## Approach

### Backend (.NET)
- **Project**: `Volterp.Tests` targeting `net10.0`
- **Stack**: xUnit 2.x, Moq 4.x, FluentAssertions 6.x, Microsoft.AspNetCore.Mvc.Testing
- **Structure**:
  - `Services/SaleServiceTests.cs`, `PurchaseServiceTests.cs`, `ProductServiceTests.cs`, `UserServiceTests.cs`
  - `Controllers/AuthControllerTests.cs`, `SalesControllerTests.cs`, `PurchasesControllerTests.cs`
  - `Repositories/ProductRepositoryTests.cs` (in-memory EF Core)
- **Naming**: `{ServiceName}Tests.cs`, methods: `MethodName_Scenario_ExpectedResult()`
- **Bug detection tests** (must FAIL before fix):
  - `CreateSaleAsync_WithValidSale_DecrementsProductStock` → FAIL (stock unchanged)
  - `CreatePurchaseAsync_WithValidPurchase_IncrementsProductStock` → FAIL (stock unchanged)

### Frontend (React)
- **Stack**: Vitest 4.1.5, @testing-library/react, MSW for API mocking
- **Structure**:
  - `src/stores/ventaStore.test.ts`, `productoStore.test.ts`, `compraStore.test.ts`
  - `src/api/fetchWithAuth.test.ts`, `saleService.test.ts`, `productService.test.ts`
  - `src/utils/jwt.test.ts`
- **Naming**: `{Feature}.test.ts`, descriptive `it('should X when Y')`
- **Strict TDD**: tests written before implementation per project config

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `Volterp/Volterp.Tests/` | New | xUnit project — greenfield, all test files |
| `Volterp/Volterp.Application/Services/` | Modified | Service tests depend on interface contracts |
| `erp-mvp/src/stores/` | Modified | New store test files for venta, producto, compra |
| `erp-mvp/src/api/` | Modified | New API service test files |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Backend greenfield — no existing patterns to follow | High | Align with clean architecture layering; use existing interface contracts |
| Frontend strict TDD — tests must lead implementation | Medium | Test the CURRENT (buggy) behavior first; assert correct behavior fails |
| Mock setup complexity for IUnitOfWork | Medium | Extract mock helpers in test base class |

## Rollback Plan

- **Backend**: Delete `Volterp.Tests/` folder, remove project reference from `Volterp.sln`. Revert: `git checkout -- Volterp.slnx`
- **Frontend**: Delete new `*.test.ts` files added to `src/stores/` and `src/api/`. Revert: `git checkout -- src/stores/ src/api/`
- No database migrations or schema changes involved.

## Dependencies

- `dotnet new xunit` (Volterp.Tests scaffolding)
- `Moq`, `FluentAssertions`, `Microsoft.AspNetCore.Mvc.Testing` NuGet packages
- `msw` (Mock Service Worker) for frontend API mocking
- Vitest already configured in erp-mvp — no additional setup needed

## Success Criteria

- [ ] `CreateSaleAsync_WithValidSale_DecrementsProductStock` FAILS before Bug #1 is fixed
- [ ] `CreatePurchaseAsync_WithValidPurchase_IncrementsProductStock` FAILS before Bug #2 is fixed
- [ ] All critical services (SaleService, PurchaseService, ProductService, UserService) have ≥1 test
- [ ] `dotnet test` passes with ≥80% coverage on targeted services
- [ ] `yarn test` passes in erp-mvp with ≥80% coverage on stores and API services
- [ ] No regressions when adding new features (CI gate)