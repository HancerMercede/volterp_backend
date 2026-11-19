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
| **Company Issuers** | `GET`/`POST`/`PUT /api/CompanyIssuers` (the issuer RNCs of the tenant: the branches that issue on its behalf) |
| **Electronic Invoices** | `GET /api/ElectronicInvoices/state` (estado fiscal del tenant), `GET /api/ElectronicInvoices/{id}/qr.svg`, `GET /api/ElectronicInvoices/{id}/qr.png`, `POST /api/ElectronicInvoices/{id}/retry` (reintenta la firma de un comprobante que quedó para revisión) |
| **Electronic Invoice Print** | `GET /api/ElectronicInvoicePrint/{id}` (printable HTML representation; accepted e-CF only) |
| **API Credentials** | `GET`/`POST /api/ApiCredentials`, `DELETE /api/ApiCredentials/{id}` (the machine credential of a client that rents the API) |
| **Electronic Invoice Sequences** | `GET`/`POST`/`PUT /api/ElectronicInvoiceSequences`, `POST /api/ElectronicInvoiceSequences/{id}/annul` (annuls the e-NCF numbers the taxpayer never used) |
| **Company Emission Mode** | `GET`/`PUT /api/CompanyEmissionMode` (how the company invoices: electronic or normal) |
| **Company Contingency** | `PUT /api/CompanyEmissionMode/contingency` (the temporary suspension of the electronic path) |
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

### El reintento de un comprobante que quedó para revisión

Un e-CF que no se pudo firmar queda **para revisión**: el número ya está atado al documento (la venta no se deshace), pero el comprobante no llegó a la DGII y no había forma de sacarlo de ahí.

`POST /api/ElectronicInvoices/{id}/retry` lo vuelve a firmar:

- La empresa sale del **token** (nunca del cuerpo) → un comprobante de otra empresa da `404`.
- **El número no se vuelve a tomar**: el comprobante conserva su e-NCF y la secuencia del emisor no se mueve → no hay hueco fiscal.
- Solo se reintenta un comprobante **en borrador y para revisión**; cualquier otro estado da `400` y no se toca.
- Si la firma vuelve a fallar, el comprobante **se queda para revisión** con el motivo nuevo, y el `400` lo dice.
- La cola es la que lo manda a la DGII después: el reintento solo lo deja firmado y esperando.

### La API alquilada

Un cliente que alquila la API **no es un usuario del ERP**: no tiene login ni contraseña, tiene **su propia credencial de máquina**.

- La credencial es una **clave opaca**: un **selector público** (`keyId`) y un **secreto** → se presenta como `Authorization: ApiKey <keyId>.<secreto>`.
- El secreto se guarda **como hash** y no se vuelve a mostrar: aparece **una sola vez**, al crearla (`POST /api/ApiCredentials`, de administrador). El listado **jamás** lleva el secreto ni su hash.
- El **tenant sale de la credencial**, nunca del cuerpo de la petición: el claim `companyId` que viaja es el de la fila encontrada.
- La credencial **se revoca, no se borra** (`DELETE /api/ApiCredentials/{id}`): lo que se emitió importa. Una vez revocada deja de autenticar.
- La credencial **no es un administrador**: no lleva rol, así que las superficies de administrador siguen cerradas para ella.
- Los orígenes que pueden llamar la API vienen de la **configuración** (`Cors:AllowedOrigins`), no de un literal en el código.

### La anulación de los rangos que no se usaron

Un rango de e-NCF que la DGII autorizó y que el contribuyente no usó **pierde** esos números si no se los anuncia: `POST /api/ElectronicInvoiceSequences/{id}/annul` (de administrador) manda a la DGII el documento **ANECF** con la cola sin usar del rango.

- El hueco de un rango es **la cola que nunca se usó**: el asignador es contiguo y la posición que declara el contribuyente es el último número usado, así que lo declarado y lo nuestro coinciden.
- El ANECF se **firma con el certificado del emisor** y se manda **solo a la DGII**.
- Si la DGII lo rechaza, el rango **queda exactamente como estaba** y la respuesta viaja al que lo pidió.
- Un rango **sin cola** no tiene nada que anular y **falla cerrado**; uno ya anulado también.
- La anulación **cierra** el rango: sus números no vuelven a servir.

### El comprobante fiscal normal (NCF en papel)

La empresa declara **cómo factura** (`GET`/`PUT /api/CompanyEmissionMode`): **electrónica** o **normal**. El valor viaja por nombre: `"Electronic"` o `"Normal"`.

- En modo **electrónico**, la venta completada toma el camino del e-CF: se carga el certificado del emisor y se habla con la DGII. Es el único camino que deja rastro ante la DGII.
- En modo **normal**, la venta completada emite el **comprobante fiscal normal**: un NCF de **once posiciones** (`B` + dos dígitos de tipo + ocho de secuencia, por ejemplo `B0100000001`), tomado del **rango propio que la DGII autorizó para los comprobantes no electrónicos**. El rango del e-CF no se lee ni se mueve.
- El tipo normal es el equivalente del electrónico (31→B01, 32→B02, 33→B03, 34→B04); los tipos de compra no tienen equivalente normal y fallan cerrados.
- El número, el documento y la finalización de la venta caen en **una sola transacción**: un número nunca se quema sin su documento, y repetir la finalización de una venta no consume otro número.
- **Sin rango autorizado, la venta no completa** (falla cerrado). El rango se carga y se consulta con `/api/NormalFiscalSequences` (el alta y la modificación son de administrador).
- **La factura comercial**: en el camino normal, el que completa la venta puede pedir que esa venta salga como **factura comercial** (`PUT /api/sales/{id}/complete` con el cuerpo opcional `{"asCommercialInvoice": true}`): sin NCF, sin documento fiscal, sin tocar ningún rango y sin nada hacia la DGII. Sin ese cuerpo, la venta sale con el comprobante fiscal normal. En el camino electrónico no aplica y falla cerrado.
- **La contingencia**: `PUT /api/CompanyEmissionMode/contingency` declara la **suspensión temporal** del camino electrónico (`Total` o `Partial`, con entrada y salida, y dentro de los **quince días** que permite la norma). Mientras la ventana está abierta, la venta sale en papel; fuera de ella, el camino electrónico vuelve. No es un modo: no reemplaza la declaración de cómo factura la empresa.
- **El e-CF que reemplaza al comprobante de papel**: el comprobante normal emitido durante la contingencia se reemplaza por un e-CF del **mismo tipo**, que se crea junto al papel y lleva su referencia en `InformacionReferencia` (`NCFModificado` = el número del papel, `FechaNCFModificado` = su fecha en formato día-mes-año y `CodigoModificacion` = **4**, el de reemplazo por contingencia). Ese e-CF sale **solo hacia la DGII** —nunca al receptor, que conserva el comprobante en papel— y **desde el levantamiento de la contingencia**: mientras la ventana está abierta espera en la cola, y no le aplican el plazo de 72 horas ni el corte de 15 días de la contingencia de la DGII. Si la contingencia sigue abierta después de los quince días que permite la norma, el reemplazo queda **para revisión** y lo toma una persona.

### Los emisores del tenant (multi-RNC)

Un tenant puede tener **varios RNC emisores**: el de la empresa y el de sus sucursales. El certificado pertenece al **RNC emisor** y no al tenant, y la venta nombra el RNC que la emite.

- El registro de emisores vive en `/api/CompanyIssuers` (`GET`, `GET {id}`, `POST` y `PUT {id}`; el alta y la modificación son de administrador). El RNC de la empresa **no** se registra: es su propia identidad, y el registro lo rechaza. Un RNC ya registrado, uno inválido o uno sin razón social también. Y un RNC que **pertenece a otro contribuyente** se rechaza: un RNC es de un solo contribuyente, así que con él el tenant emitiría en nombre de otro. El índice de la base lo garantiza (el segundo tenant que lo registra no lo obtiene) y la atribución de un documento recibido **falla cerrada** cuando el RNC del receptor aparece en dos tenants.
- La venta lleva el `issuerTaxId` opcional (`POST`/`PUT /api/sales`): vacío significa que emite la empresa; con valor, tiene que ser **uno de los emisores del tenant** (falla cerrado).
- El e-CF y el comprobante normal salen con **el RNC de la venta**: el número se toma del rango de ese RNC, se firma con **su** certificado y el bloque del emisor del XML lleva **su** RNC y **su** razón social.
- El certificado se carga con el RNC de un emisor del tenant (`/api/CompanyCertificates`), y el rango también (`/api/ElectronicInvoiceSequences` y `/api/NormalFiscalSequences`).
- La recepción de un e-CF de un tercero atribuye el documento al tenant cuyo RNC —el propio o el de una sucursal— aparece como receptor. Si dos tenants registran el mismo RNC, la atribución **falla cerrado** por ambigua.
- Una sucursal desactivada **no emite**: su RNC deja de ser un emisor válido y la venta que lo nombra falla cerrada. El RNC de un emisor no se cambia: el certificado y los rangos autorizados están atados a él.

### El circuito contra la DGII de prueba

El E2E contra la DGII real corre **solo** con las variables de entorno puestas: sin ellas los dos tests se saltan (no es un fallo). El `.p12` y su contraseña **nunca** viajan en el repositorio.

| Variable | Qué es |
|---|---|
| `VOLTERP_DGII_BASE_URL` | la base de la DGII (`https://ecf.dgii.gov.do`) |
| `VOLTERP_DGII_ENVIRONMENT` | el ambiente: `PreCertification` (`testecf`), `Certification` (`certecf`), `Production` (`ecf`) |
| `VOLTERP_DGII_CERTIFICATE` | la ruta del `.p12` del contribuyente |
| `VOLTERP_DGII_CERTIFICATE_PASSWORD` | la contraseña del `.p12` |
| `VOLTERP_DGII_ISSUER_RNC` | el RNC (o la cédula) del contribuyente que emite |
| `VOLTERP_DGII_ENCF` | el e-NCF del rango que la DGII autorizó (13 posiciones: `E` + 2 del tipo + 10 de secuencia) |

En la **pre-certificación** la DGII da a cada contribuyente habilitado un rango por tipo: el 31 va de `E310000000001` a `E310010000000` y el 32 de `E320000000001` a `E320050000000`, con vencimiento el **31-12-2028** ("Descripción Técnica Servicios DGII"). El documento del circuito es un e-CF de verdad —de crédito fiscal (31), construido como lo construye el ERP y validado contra el esquema oficial—, porque si no el veredicto de la DGII hablaría de la forma del documento y no del contribuyente.

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
