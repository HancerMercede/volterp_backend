# Volterp ERP — Backend API

ERP backend built with Clean Architecture, .NET 10, and PostgreSQL. Serves as the API layer for the Volterp ERP frontend ([erp-mvp](https://github.com/HancerMercede/erp-mvp)).

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Runtime | .NET 10 |
| ORM | EF Core 10 (Npgsql) |
| Database | PostgreSQL |
| Auth | JWT (bearer token) |
| Mapping | [MapFlow](https://github.com/HancerMercede/MapFlow) |
| Testing | xUnit, Moq, FluentAssertions |

## Architecture

Clean Architecture with four layers:

```
Volterp.slnx
├── Volterp.Domain       — Entities, enums, business rules
├── Volterp.Application  — DTOs, service interfaces, service implementations
├── Volterp.Infrastructure — EF Core DbContext, repositories, unit of work, migrations
├── Volterp.Api          — Controllers, middleware, configuration, Program.cs
└── Volterp.Tests        — xUnit unit tests for application services
```

### Domain (`Volterp.Domain`)

Entities with audit support and enums.

**Entities:**
`User`, `Employee`, `Client`, `Supplier`, `Product`, `Category`, `Sale`, `SaleItem`, `Purchase`, `PurchaseItem`, `Company`, `AccountingTransaction`

**Enums:**
`UserRole`, `SaleStatus`, `TransactionType`, `EntityStatus`

### Application (`Volterp.Application`)

Service layer with interfaces and implementations. DTOs for each entity. Uses `IServiceManager` as the service aggregation root.

**Services:**
`UserService`, `EmployeeService`, `ClientService`, `SupplierService`, `ProductService`, `CategoryService`, `SaleService`, `PurchaseService`, `CompanyService`, `AccountingTransactionService`

### Infrastructure (`Volterp.Infrastructure`)

EF Core `VolterpDbContext`, repository pattern via `IRepositoryBase<T>`, `IUnitOfWork` for transaction coordination.

### API (`Volterp.Api`)

RESTful controllers, JWT authentication, CORS configuration.

**Controllers:**
`AuthController`, `UsersController`, `EmployeesController`, `ClientsController`, `SuppliersController`, `ProductsController`, `CategoriesController`, `SalesController`, `PurchasesController`, `CompaniesController`, `AccountingTransactionsController`

### Tests (`Volterp.Tests`)

Unit tests covering the critical application services:
- `SaleServiceTests` — stock decrement on sale completion
- `PurchaseServiceTests` — stock increment on purchase creation
- `ProductServiceTests` — product CRUD and stock queries
- `UserServiceTests` — authentication and user management
- `CompanyServiceTests` — company configuration

## Getting Started

### Prerequisites

- .NET 10 SDK
- PostgreSQL 16+

### Setup

1. Clone the repo:
   ```bash
   git clone https://github.com/HancerMercede/volterp_backend.git
   cd volterp_backend
   ```

2. Configure the database and JWT settings in `Volterp.Api/appsettings.json`. The file is already present in the repo with development defaults — override via environment variables or user secrets for production.

3. Apply migrations (they run automatically on startup, or manually):
   ```bash
   dotnet ef database update
   ```

4. Run the API:
   ```bash
   dotnet run --project Volterp.Api
   ```

### Running Tests

```bash
dotnet test
```

## API Overview

All endpoints are prefixed with `/api`. Protected endpoints require a JWT Bearer token.

| Area | Endpoints |
|------|-----------|
| **Auth** | `POST /api/auth/login`, `POST /api/auth/register` |
| **Users** | CRUD `/api/users` |
| **Employees** | CRUD `/api/employees` |
| **Clients** | CRUD `/api/clients` |
| **Suppliers** | CRUD `/api/suppliers` |
| **Products** | CRUD `/api/products` |
| **Categories** | CRUD `/api/categories` |
| **Sales** | CRUD `/api/sales` |
| **Purchases** | CRUD `/api/purchases` |
| **Companies** | CRUD `/api/companies` |
| **Accounting** | CRUD `/api/accounting-transactions` |

## Related

- [Frontend (erp-mvp)](https://github.com/HancerMercede/erp-mvp) — React SPA
- [MapFlow](https://github.com/HancerMercede/MapFlow) — DTO mapping library
