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

   `Encryption:MasterKey` is the 256-bit base64 key that encrypts the issuer credentials at rest. It is **not** rotatable in place: changing it makes every stored credential unreadable. Generate a production one with `openssl rand -base64 32` and inject it as `Encryption__MasterKey`.

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
| **Company Certificates** | `GET /api/CompanyCertificates`, `POST /api/CompanyCertificates` (admin) |
| **Electronic Invoices** | `GET /api/ElectronicInvoices/state` (estado fiscal del tenant), `GET /api/ElectronicInvoices/{id}/qr.svg`, `GET /api/ElectronicInvoices/{id}/qr.png` |
| **Electronic Invoice Print** | `GET /api/ElectronicInvoicePrint/{id}` (printable HTML representation; accepted e-CF only) |
| **Company Emission Mode** | `GET`/`PUT /api/CompanyEmissionMode` (how the company invoices: electronic or normal) |
| **Electronic Invoice Sequences** | `GET`/`POST`/`PUT /api/ElectronicInvoiceSequences` (the e-NCF ranges the DGII authorized) |
| **Normal Fiscal Sequences** | `GET`/`POST`/`PUT /api/NormalFiscalSequences` (the normal-CNF ranges the DGII authorized) |
| **Electronic Invoice Reception** | `POST /fe/recepcion/api/ecf`, `POST /fe/aprobacioncomercial/api/ecf` (the DGII calls these) |
| **Accounting** | CRUD `/api/accounting-transactions` |

### Errors

Every error of the API travels in the same envelope: the status says what happened and the body says why.

- **4xx** → `application/json`, `{"error": "...", "details": "..."}`. `details` is optional and carries the extra context when there is any.
- **5xx** → `application/problem+json` (RFC 7807): `type`, `title`, `status`, `detail`, `traceId` and `timestamp`. Only an unhandled exception arrives here; a business rule never does.
- **429** → the same 4xx envelope as the rest of the API, with the `Retry-After` header.

The status separates the two failures that used to look alike:

| Situation | Status |
|---|---|
| The target does not exist (the sale, the client, the range) | `404 Not Found` |
| The target exists but the request breaks a rule (a completed sale cannot be updated, the sale has no items, the taxpayer has no authorized normal range) | `400 Bad Request` |
| The caller is not authenticated | `401 Unauthorized` |
| The caller is authenticated but not allowed | `403 Forbidden` |
| The rate limit rejected the request | `429 Too Many Requests` |

So `PUT /api/sales/{id}/complete` answers `404` when the sale does not exist and `400` when the sale exists and the completion breaks a rule.

`GET /api/ElectronicInvoices/state` contesta el estado fiscal del tenant (la empresa sale del token, nunca de la petición):

```json
{
  "counts": {
    "pendingEmission": 2,
    "inProcess": 1,
    "rejected": 0,
    "requiresReview": 1,
    "total": 4
  },
  "invoices": [
    {
      "id": "0192…",
      "saleId": "0192…",
      "encf": "E320000000001",
      "documentType": "Consumer",
      "fiscalState": "PendingEmission",
      "status": "Signed",
      "verdictNote": null,
      "requiresReview": false,
      "sentAt": null,
      "nextCheckAt": null,
      "statusCheckCount": 0,
      "rawResponse": null,
      "sequenceConsumed": null,
      "receivedAt": null,
      "lastError": null
    }
  ]
}
```

- `fiscalState` es la clasificación del estado fiscal: `PendingEmission` (firmado y esperando en la cola, todavía no salió hacia la DGII), `InProcess` (la DGII lo tiene y todavía no lo validó), `Rejected` (la DGII lo rechazó) o `RequiresReview` (no se pudo firmar, así que no llegó a la DGII). Un comprobante ya cerrado no tiene estado fiscal.
- `?limit` (default 50) acota el listado; el conteo siempre cubre todos los comprobantes abiertos.
- `rawResponse` es el cuerpo crudo de la última respuesta de la DGII (ahí quedan los códigos de sus mensajes), `sequenceConsumed` es lo que la DGII dice del número (si el rechazo lo volvió inservible), `receivedAt` es cuándo lo recibió y `lastError` el motivo del último fallo al hablar con la DGII.
- El cuerpo de la respuesta tiene un **tope de 4000 caracteres**: lo que sobra termina en `... [truncated]`. Una respuesta patológica (o el HTML de un gateway) no puede hacer crecer la fila ni el payload, y el corte no rompe nada: el e-CF sigue su curso.
- El cuerpo de la **conversación del token** nunca se guarda: puede llevar el token de la DGII, que es una credencial. Lo que sale por aquí es el cuerpo del **envío** y del **veredicto** (lo que el operador necesita ver) y sin credenciales: los valores con forma de token van redactados como `[redacted]`.

### El comprobante fiscal normal (NCF en papel)

La empresa declara **cómo factura** (`GET`/`PUT /api/CompanyEmissionMode`): **electrónica** o **normal**. El valor viaja por nombre: `"Electronic"` o `"Normal"`.

- En modo **electrónico**, la venta completada toma el camino del e-CF: se carga el certificado del emisor y se habla con la DGII. Es el único camino que deja rastro ante la DGII.
- En modo **normal**, la venta completada emite el **comprobante fiscal normal**: un NCF de **once posiciones** (`B` + dos dígitos de tipo + ocho de secuencia, por ejemplo `B0100000001`), tomado del **rango propio que la DGII autorizó para los comprobantes no electrónicos**. El rango del e-CF no se lee ni se mueve.
- El tipo normal es el equivalente del electrónico (31→B01, 32→B02, 33→B03, 34→B04); los tipos de compra no tienen equivalente normal y fallan cerrados.
- El número, el documento y la finalización de la venta caen en **una sola transacción**: un número nunca se quema sin su documento, y repetir la finalización de una venta no consume otro número.
- **Sin rango autorizado, la venta no completa** (falla cerrado). El rango se carga y se consulta con `/api/NormalFiscalSequences` (el alta y la modificación son de administrador).

### El QR de la representación impresa

La DGII pide la **versión 8** del símbolo para la representación impresa ("Descripción Técnica Servicios DGII", p. 41-42), y el nivel de corrección de errores (Q) es otra cosa: el código la pedía explícitamente. La URL del timbre es más larga de lo que la versión 8 admite con ese nivel (108 bytes), así que **el símbolo crece** hasta el que la codifica: un QR que no codifica la URL sirve menos que uno que no lleva la versión que nombra la DGII.

### El `Down` de la migración de las respuestas de la DGII

La migración `20261114221020_AddDgiiResponses` (T47) agregó las cuatro columnas con lo que respondió la DGII: `RawResponse`, `SequenceConsumed`, `ReceivedAt` y `LastError`. Su `Down` **borra las respuestas registradas**: revertir la migración deja los comprobantes sin la respuesta que les dio la DGII (y sin la fecha de recepción, sin lo que la DGII dijo del número y sin el motivo del último fallo). Es un rollback destructivo sobre datos fiscales: copie la base antes de revertirla.

La credencial del certificado se guarda cifrada y nunca vuelve por la respuesta. Para firmar e-CF, el ERP la interpreta como el sobre `{"pfx": "<base64 del .p12>", "password": "<password del .p12>"}`: es el único secreto del emisor, porque la DGII autentica los web services con la firma del certificado.

### DGII certificate authentication

The reception endpoints (`POST /fe/recepcion/api/ecf` and `POST /fe/aprobacioncomercial/api/ecf`) use the `DgiiCertificate` authentication scheme. TLS must present a client certificate; the handler rejects a missing certificate, an expired/not-yet-valid certificate, a certificate without the client-authentication EKU, and a certificate whose chain cannot be built to a configured trust anchor.

Configure the anchors as absolute certificate-file paths (PEM or DER):

```bash
DgiiCertificate__TrustedRootCertificatePaths__0=/secure/path/dgii-root.pem
```

The default trust mode is `CustomRootTrust`. With no configured anchor every certificate request is rejected. `System` mode proves only that the operating-system trust store accepts the chain; it does **not** prove that the certificate belongs to the DGII. The official DGII trust anchor and the registration of the exact certificate used in each taxpayer's certification remain an external operational dependency (L78).

## Related

- [Frontend (erp-mvp)](https://github.com/HancerMercede/erp-mvp) — React SPA
- [MapFlow](https://github.com/HancerMercede/MapFlow) — DTO mapping library
