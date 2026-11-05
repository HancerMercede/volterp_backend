# Demo seed data (Northwind-style, multi-tenant)

Feature: seed de datos demo por empresa, aplicado como migración de EF Core.

## Objective

- **Feature**: `demo-seed-data`
- **Branch**: `feature/electronic-invoicing` (HEAD `a162cf6` al inicio)
- **Delivery**: un work unit — la migración de datos + su verificación
- **Test runner**: `dotnet test Volterp.Tests/Volterp.Tests.csproj` + `dotnet ef database update` contra el Postgres local
- **Objetivo**: datos demo coherentes y realistas en 5 empresas para poder loguearse y "jugar con el sistema" como en Northwind

## Specs

### S1
> "al menos 5 empresas para empezar"

La migración crea **5 empresas** nuevas (además de la ya sembrada `HM Software Solutions`, que no se toca). Cada empresa es un tenant completo: sus propios usuarios, catálogo, clientes, proveedores, ventas, compras, empleados y asientos contables.

### S2
> "crear una migracion para eso"

El seed se implementa como **una migración de EF Core** (`AddDemoSeedData`) en `Volterp.Infrastructure/Migrations/`, no como un seeder de runtime. Se aplica con `dotnet ef database update` y queda versionada junto al schema. Es data-only: no cambia el modelo, no toca el snapshot de forma significativa.

### S3
> "un seed de data para tener informacion que mostrar en el sistema y jugar con ella, para cada tabla etc."

Todas las tablas operativas quedan pobladas y **coherentes entre sí**: cada venta apunta a un cliente y a productos que existen en la misma empresa, cada compra a un proveedor existente, cada venta genera sus `SaleItem` con el producto real. Volumen por empresa:

| Tabla | Por empresa | Total (5) |
|---|---|---|
| `Companies` | 1 | 5 |
| `Users` | 3 (Admin, Ventas, Contabilidad) | 15 |
| `Categories` | 5 | 25 |
| `Products` | 10 | 50 |
| `Clients` | 8 | 40 |
| `Suppliers` | 5 | 25 |
| `Sales` | 8 | 40 |
| `SaleItems` | ~18 (2-3 por venta) | ~90 |
| `Purchases` | 5 | 25 |
| `PurchaseItems` | ~12 (2-3 por compra) | ~60 |
| `Employees` | 6 | 30 |
| `AccountingTransactions` | 15 | 75 |
| `ElectronicInvoiceSequences` | 2 | 10 |
| `ElectronicInvoices` | 3 (en `Draft`) | 15 |

### S4
> "como si fuera un norwind"

Datos **comercialmente creíbles**, no filas de relleno: nombres de empresa dominicanos, precios en ARS, ITBIS 18% calculado sobre el subtotal (`Subtotal + Itbis = Total`), proveedores con rubro, empleados con puesto y departamento, asientos con tipo (Debit/Credit), categoría y referencia. Fechas repartidas en los últimos ~90 días.

### S5
Los usuarios demo **se pueden loguear** con credenciales conocidas: `demo.admin1` / `Demo123!`, `demo.ventas1` / `Demo123!`, `demo.contabilidad1` / `Demo123!` (y equivalentes `2`..`5` por empresa). Hash BCrypt costo 12, un hash distinto por usuario. El login usa `POST /api/auth/login` con `username` + `password`.

### S6
La migración es **reversible**: `Down()` borra exactamente lo que `Up()` insertó, y nada más. Ninguna fila preexistente se ve afectada.

### S7
La migración **no colisiona** con datos existentes ni los altera. Identificadores demo reconocibles y no colisionables:
- `Companies.TaxId`: RNC de 9 dígitos **válidos** (dígito verificador según `Volterp.Domain/ValueObjects/Rnc.cs`, pesos `[7,9,8,6,5,4,3,2]`). `Companies.TaxId` es `string` plano, no hay VO en la lectura.
- `Users.Username`: `demo.<rol><n>`; `Users.Email`: `demo.<rol><n>@volterp.demo`.
- `Clients.Email`: `<cliente><n>.empresa<n>@volterp.demo`.
- `Categories`: el índice único es `(Name, CompanyId)` → el mismo nombre de categoría puede repetirse en empresas distintas.
- `ElectronicInvoiceSequences`: el índice único es `(CompanyId, DocumentType)` → una fila por empresa y tipo.
- La fila `Companies.Id = 00000001-…-0001` y `Users.Id = 00000002-…-0001` (sembradas por `HasData`) **no se tocan**.

### S8
Restricciones técnicas que la migración debe respetar:
- **Todos los `Id` son client-supplied** (Guid, sin default de DB) → la migración los fija explícitamente, de forma determinista y sin colisión.
- `CreatedAt` **no** es `ValueGeneratedOnAdd` → se fija explícitamente en cada fila.
- Enums: `UserRole`, `SaleStatus`, `TaxCode`, `EcfDocumentType`, `EcfStatus`, `Currency` persisten como **int**; `EntityStatus` y `TransactionType` como **string**.
- `Product.Price` es una propiedad compleja → **dos** columnas: `Price` (numeric 18,2) y `Currency` (int).
- `Sale.ClienteId` y `SaleItem.ProductId` **no** tienen FK (solo índice) → se pueden sembrar igual, pero se siembran con IDs que sí existen.
- `Money` lanza con montos negativos → todos los montos son positivos.

### S9
La verificación es contra el **Postgres real** (localhost:5432, base `volterp`), no contra SQLite de tests: aplicar la migración, contar filas por tabla, y probar el login de un usuario demo por HTTP.

## Tasks

| ID | S# linked | Route | Status | Commit / evidence |
|---|---|---|---|---|
| T1 | S1-S9 | inline (parent) | DONE | este documento |
| T2 | S2-S8 | delegated (`gentle-ai-worker`) | DONE | `20261105154111_AddDemoSeedData` (675 líneas) aplicada y revertida contra el Postgres real |
| T3 | S5, S9 | inline (parent) | DONE | spot check del padre: 507 filas contadas en el Postgres real y login HTTP `demo.admin1` → 200 + JWT |

## Log

### L1 — 2026-11-05
> "champ otra cosa podemos crear un seed de data para tener informacion que mostrar en el sistema y jugar con ella, para cada tabla etc. como si fuera un norwind"

### L2 — 2026-11-05
> "crear una migracion para eso"

### L3 — 2026-11-05
> "al menos 5 empresas para empezar"

### L4 — 2026-11-05 — decisiones de diseño (parent)
- Migración **data-only** con `migrationBuilder.Sql` y `INSERT ... VALUES (...),(...)` multi-fila: ~500 filas caben en ~14 sentencias legibles, en vez de miles de líneas de arrays `InsertData`.
- No se siembran `CompanyCertificates` (requieren un `.p12` real cifrado) ni `ElectronicInvoice` con firma real: los `ElectronicInvoice` se siembran en `Draft` solo si la columna `SignedXml` admite un placeholder sin romper la lectura.
- El RNC de cada empresa se calcula con el algoritmo de `Rnc.cs`, no se inventa a mano.
- `Risk: item 1 (datos persistidos) + item 5 (entorno)` — la migración corre en el `Migrate()` de arranque de todo entorno no-`Testing`. Hoy el repo no tiene `appsettings.Production.json`, ni CI, ni Docker (verificado), así que el impacto real es local. Mitigación: identificadores demo imposibles de colisionar y `Down()` exacto.

### L5 — 2026-11-05 — T2: la migración `AddDemoSeedData` (delegado)
- **Qué**: `Volterp.Infrastructure/Migrations/20261105154111_AddDemoSeedData.cs` (675 líneas) siembra 505 filas con `migrationBuilder.Sql` y una sentencia `INSERT ... VALUES` por tabla (14 sentencias), en 5 tenants.
- **Volumen verificado en el Postgres real**: 5/15/25/50/40/25 companies/users/categories/products/clients/suppliers, 40 ventas + 90 items, 25 compras + 60 items, 30 empleados, 75 asientos, 10 secuencias y 15 e-facturas. Total 507 filas contando las 2 preexistentes.
- **Identificadores**: un solo prefijo demo `0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e01` en adelante, correlativo entre tablas; nunca toca `00000001-…-0001` (empresa) ni `00000002-…-0001` (usuario `admin`).
- **RNC**: 9 dígitos válidos calculados con los pesos de `Rnc.cs` y verificados con `Rnc.TryParse` (`101500018`, `101500026`, `101500034`, `101500042`, `101500052`).
- **Coherencia**: toda venta apunta a cliente y productos de su misma empresa, `Cantidad x Precio = Subtotal`, `ITBIS = Subtotal x 0.18` y `Total = Subtotal + ITBIS`; las consultas de control devuelven 0 filas en las 14 comprobaciones.
- **`Down()`**: borra por `CompanyId` de los tenants demo y por lista explícita de `Id` en `Companies`, `Users`, `SaleItems` (por `SaleId`) y `PurchaseItems` (por `PurchaseId`). Probado: 507 filas antes → 2 después (solo las preexistentes, con el mismo `PasswordHash`).
- **Decisión — `ElectronicInvoices`**: se siembran las 15 filas en `Draft` con un `SignedXml` placeholder bien formado. No hay ningún camino de lectura que analice o valide esa columna: la API y la Application no exponen `ElectronicInvoice` y el `Verify` de `XmlDsigEcfSigner` trabaja sobre un string en memoria, no sobre la fila. La columna es `NOT NULL`, así que el placeholder es obligatorio para sembrarla. `SignedAt`, `TrackId`, `SecurityCode`, `QrUrl`, `SentAt` y `VerdictNote` van en `NULL`.
- **Decisión — `CompanyCertificates`**: la tabla queda intacta; requiere un `.p12` real cifrado que la migración no puede fabricar.
- **Credenciales**: `demo.admin{n}`, `demo.ventas{n}`, `demo.contabilidad{n}` con `n` de 1 a 5, contraseña `Demo123!`, BCrypt costo 12 y un hash distinto por usuario (los 15 verificados contra la fila real de la base).
- **Fechas**: fijas a un instante de referencia (`2026-11-05T12:00Z`) para que la migración sea reproducible; los datos operativos caen en los ~90 días previos.

### L6 — 2026-11-05 — T3: verificación del padre (spot check, independiente del worker)
- **Conteos releídos por el padre** contra el Postgres real (`docker exec volterp-db psql`): Companies 6, Users 16, Categories 25, Products 50, Clients 40, Suppliers 25, Sales 40, SaleItems 90, Purchases 25, PurchaseItems 60, Employees 30, AccountingTransactions 75, ElectronicInvoiceSequences 10, ElectronicInvoices 15, CompanyCertificates 0. Coincide con lo reportado.
- **Preexistentes intactos**: `Companies` `HM Software Solutions` (`00000001-…-0001`, TaxId `HM123456789`) y `Users` `admin` con el mismo `PasswordHash` `$2a$12$XLgan9fXGIGfO.UcBuUweegkVWrYJqDgOZY48h4/udZJyNASNol3O`.
- **Coherencia releída por el padre**: items de venta que apuntan a un producto de otra empresa = 0; ventas con `Total <> Subtotal + Itbis` = 0; compras con `Total` distinto de la suma de sus items = 0; drift `Subtotal - Quantity x UnitPrice` = 0.00; montos no positivos = 0; `CreatedAt` nulos = 0.
- **Login HTTP real**: la API arrancada contra el Postgres real (`http://localhost:5106`) respondió `POST /api/auth/login` con `demo.admin1` / `Demo123!` → **200** y JWT con claims `demo.admin1`, `demo.admin1@volterp.demo`, `Administrador Demo 1`, rol `Admin`, `companyId 0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e01`. Es la prueba de que el seed sirve para "jugar con el sistema": el token claims la empresa del usuario.
- **Gotcha operativo**: `dotnet ef database update --startup-project Volterp.Api --no-build` carga el assembly de `Volterp.Api\bin`, no el del proyecto objetivo; una copia vieja registró la migración en `__EFMigrationsHistory` sin ejecutar su `Up()`. Hay que reconstruir la API antes de aplicar.
