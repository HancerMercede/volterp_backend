# Design: Volterp ERP Test Infrastructure

## Technical Approach

Establish comprehensive test infrastructure for Volterp ERP covering both backend (.NET) and frontend (React) layers. Backend tests use xUnit + Moq + FluentAssertions with dependency injection via constructor mocks. Frontend tests use existing Vitest setup with vi.fn() mocks for store and API testing.

## Architecture Decisions

### Decision: Backend Test Project Location

**Choice**: `Volterp.Tests/` sibling to existing projects in `source/repos/Volterp/`
**Alternatives considered**: Inside Volterp.Api (couples tests to web project), separate solution (unnecessary complexity)
**Rationale**: Sibling folder to main solution keeps test project co-located but cleanly separated. Follows .NET convention of `Project.Tests` naming.

### Decision: Service Test Mocking Strategy

**Choice**: Mock IUnitOfWork + typed repository interfaces completely
**Alternatives considered**: Test doubles for repositories, snapshot testing
**Rationale**: IUnitOfWork is the composition root for services. Mocking at this boundary isolates service logic from persistence concerns. Typed repositories (ISaleRepository, IProductRepository) allow precise verification of interactions.

### Decision: Bug Test Assertion Pattern

**Choice**: Assert that buggy behavior currently passes, proving the bug exists
**Alternatives considered**: Assert correct behavior (will fail until bug fixed), skip bug tests
**Rationale**: Tests that document known bugs serve as regression protection. The assertion `product.Stock.Should().Be(10)` proves the decrement never happens. When the bug is fixed, this test will fail — signaling the fix worked.

### Decision: Frontend Test File Location

**Choice**: Extend existing `src/test/` directory with new `stores/` and `api/` test files
**Alternatives considered**: `__tests__` co-located with source files, separate `tests/` at root
**Rationale**: Existing `src/test/` structure already configured in vite.config.ts. Adding `stores/` and `api/` subdirectories maintains organization without disrupting existing component tests.

## Data Flow

```
Backend (.NET):
┌─────────────────────────────────────────────────────┐
│                  Volterp.Tests                       │
│                                                      │
│  Service Tests (Unit)        Controller Tests       │
│  ┌──────────────────────┐   ┌──────────────────────┐│
│  │ Mock<IUnitOfWork>    │   │ WebApplicationFactory││
│  │ Mock<ISaleRepository>│   │  + Real DbContext    ││
│  │ Mock<IProductRepo>   │   │  + Mock Auth Claims  ││
│  └──────────────────────┘   └──────────────────────┘│
│                                                      │
│  Infrastructure Tests (Unit)   Repository Tests     │
│  ┌──────────────────────┐   ┌──────────────────────┐│
│  │ Mock dependencies    │   │  Testcontainers      ││
│  │ Call real impl       │   │  or InMemory Db      ││
│  └──────────────────────┘   └──────────────────────┘│
└─────────────────────────────────────────────────────┘

Frontend (React/Vitest):
┌─────────────────────────────────────────────────────┐
│              src/test/ (Vitest + jsdom)              │
│                                                      │
│  Store Tests                API Tests                │
│  ┌──────────────────────┐   ┌──────────────────────┐│
│  │ vi.fn() mocks on     │   │ Mock fetchWithAuth   ││
│  │ saleService.*        │   │ 401 → logout flow    ││
│  │ Zustand.setState     │   │                      ││
│  │ reset between tests  │   │                      ││
│  └──────────────────────┘   └──────────────────────┘│
│                                                      │
│  Utils Tests                Component Tests          │
│  ┌──────────────────────┐   ┌──────────────────────┐│
│  │ Pure functions       │   │ @testing-library     ││
│  │ JWT decode/encode    │   │ already exist in     ││
│  │                      │   │ src/test/components/ ││
│  └──────────────────────┘   └──────────────────────┘│
└─────────────────────────────────────────────────────┘
```

## File Changes

### Backend (.NET) — New Files

| File | Action | Description |
|------|--------|-------------|
| `Volterp.Tests/Volterp.Tests.csproj` | Create | xUnit + Moq + FluentAssertions project |
| `Volterp.Tests/Services/SaleServiceTests.cs` | Create | Unit tests for SaleService CRUD + bug reproduction |
| `Volterp.Tests/Services/PurchaseServiceTests.cs` | Create | Unit tests for PurchaseService |
| `Volterp.Tests/Services/UserServiceTests.cs` | Create | Unit tests for UserService |
| `Volterp.Tests/Services/ProductServiceTests.cs` | Create | Unit tests for ProductService |
| `Volterp.Tests/Infrastructure/PasswordHasherTests.cs` | Create | Unit tests for hashing + legacy verify |
| `Volterp.Tests/Infrastructure/JwtServiceTests.cs` | Create | Unit tests for token generation |
| `Volterp.Tests/Controllers/SalesControllerTests.cs` | Create | Integration tests with WebApplicationFactory |
| `Volterp.Tests/Base/ServiceTestBase.cs` | Create | Generic base for service tests with mock setup |
| `Volterp.Tests/Base/ControllerTestBase.cs` | Create | WebApplicationFactory wrapper with auth |

### Frontend (React) — New Files

| File | Action | Description |
|------|--------|-------------|
| `src/test/stores/ventaStore.test.ts` | Create | Zustand store tests for sales |
| `src/test/stores/productoStore.test.ts` | Create | Zustand store tests for products |
| `src/test/api/fetchWithAuth.test.ts` | Create | 401 handling + logout dispatch tests |
| `src/test/api/saleService.test.ts` | Create | API service method tests with mocks |
| `src/test/utils/jwt.test.ts` | Create | JWT decode utility tests |

## Interfaces / Contracts

### Backend Test Base Classes

```csharp
// Volterp.Tests/Base/ServiceTestBase.cs
public abstract class ServiceTestBase<TService>
{
    protected Mock<IUnitOfWork> UnitOfWork { get; }
    protected TService Service { get; }
    
    protected void SetupRepository<TRepo>(Mock<TRepo> mock) where TRepo : class;
    protected void SetupCommit() => UnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0);
}

// Volterp.Tests/Base/ControllerTestBase.cs
public abstract class ControllerTestBase : IClassFixture<WebApplicationFactory<Program>>
{
    protected readonly WebApplicationFactory<Program> Factory;
    protected HttpClient CreateAuthenticatedClient(int companyId, string role);
}
```

### Frontend Test Patterns

```typescript
// Store test pattern (Zustand)
beforeEach(() => {
  useVentaStore.setState({ ventas: [], loading: false, error: null });
  vi.clearAllMocks();
});

// API mock pattern
vi.mock('../../infrastructure/api/saleService', () => ({
  saleService: {
    getSales: vi.fn(),
    createSale: vi.fn(),
  }
}));
```

## Testing Strategy

| Layer | What to Test | Approach |
|-------|-------------|----------|
| **Backend Services** | Business logic, bug reproduction, happy/sad paths | Unit tests with fully mocked IUnitOfWork |
| **Backend Infrastructure** | PasswordHasher.Verify, JwtService.GenerateToken | Unit tests mocking IConfiguration |
| **Backend Controllers** | HTTP status codes, auth filtering, response shape | Integration tests with WebApplicationFactory |
| **Frontend Stores** | State transitions, async actions, error handling | Zustand setState + vi.fn() mocks |
| **Frontend API** | fetchWithAuth 401 handling, service methods | Mock global fetch, verify logout call |
| **Frontend Utils** | Pure functions (JWT decode, pagination calc) | Direct function calls with assertions |

### Critical Bug Test Example

```csharp
[Fact]
public void CreateSaleAsync_WithValidInput_DoesNotDecrementProductStock()
{
    // ARRANGE
    var mockUnitOfWork = new Mock<IUnitOfWork>();
    var mockSalesRepo = new Mock<ISaleRepository>();
    var mockProductsRepo = new Mock<IProductRepository>();
    
    var product = new Product { Id = 1, Stock = 10, CompanyId = 1 };
    
    mockProductsRepo.Setup(r => r.GetProductByIdAsync(1, It.IsAny<CancellationToken>()))
        .ReturnsAsync(product);
    mockSalesRepo.Setup(r => r.AddSaleAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync((Sale s, CancellationToken ct) => s);
    
    mockUnitOfWork.Setup(u => u.Sales).Returns(mockSalesRepo.Object);
    mockUnitOfWork.Setup(u => u.Products).Returns(mockProductsRepo.Object);
    mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
    
    var service = new SaleService(mockUnitOfWork.Object);
    var request = new CreateSaleRequest(CompanyId: 1, ...);
    
    // ACT
    var result = service.CreateSaleAsync(request).Result;
    
    // ASSERT — proves bug exists (stock unchanged)
    product.Stock.Should().Be(10); // Bug: should be 5
    mockProductsRepo.Verify(
        r => r.UpdateProductAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), 
        Times.Never); // Bug: never called
}
```

### CI Compatibility

Both test suites run independently:
- `dotnet test Volterp.Tests/` — backend
- `yarn test` (vitest) — frontend

CI pipeline must pass both. No cross-platform concerns since all tests run on the same agent.

## Migration / Rollout

No migration required. This is pure test infrastructure addition.

Phased rollout:
1. Create Volterp.Tests project + base classes
2. Add service unit tests (SaleService first — highest risk of bug)
3. Add frontend store + API tests
4. Add controller integration tests last

## Open Questions

- [ ] Should controller integration tests use Testcontainers for real PostgreSQL or InMemoryDatabase? (InMemory is faster but doesn't catch EF quirks)
- [ ] Any existing bug reports to add as regression tests? The stock decrement bug is documented — others may exist
- [ ] Frontend: should we add MSW for integration-level API mocking, or is vi.fn() sufficient for unit tests?