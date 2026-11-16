# Sistema de roles y permisos — matriz en la API, roles múltiples, deny-by-default

Feature: `roles-permissions`
Branch: `feature/roles-permissions` (a crear al abrir la feature; el plan vive en el repo desde el 2026-11-07 y la rama activa hoy es `feature/electronic-invoicing`)
Delivery: por definir al abrir la feature (estrategia `ask-on-risk`)
Test runner: `dotnet test Volterp.Tests/Volterp.Tests.csproj`

Objetivo: que el sistema de roles deje de ser un enum único con guards binarios `Admin/SuperAdmin` sobre endpoints sueltos, y pase a ser tres cosas: **roles múltiples por usuario**, **matriz rol→permisos en código dentro de la API**, y **cada endpoint declarando su permiso (deny-by-default)**.

Conductor: el módulo POS (cajero, turno, caja cuadrada) es lo que hace insuficiente el modelo actual. Un "vendedor" que también opera la caja no cabe en un `User.Role` único, y "el cajero solo accede al POS" es imposible de expresar mientras todo endpoint autenticado lea todo.

**Estado: PLAN. Nada implementado. Todas las tareas están PENDING.**

## Specs

### S1
> "Bueno como quedaria la matrix en la api porque no pasarlo a la db"

La matriz rol→permiso es **código en la API** (capa Domain), no una tabla. El catálogo de permisos también es un enum cerrado en código. La base de datos guarda **solo la asignación** de roles al usuario.

Razón: `git` es el rastro de auditoría y el camino de rollback de la configuración de autorización. Con la matriz en la base, la app y la matriz se despliegan por separado (ventana de inconsistencia), un `UPDATE` de una fila cambia la autorización de toda la empresa en caliente sin review, y una caída de la base se convierte en una caída de autorización.

*Supuesto (no confirmado por el usuario)*: la matriz base es igual para todas las empresas. Si cada empresa puede customizarla, se agrega una tabla de **overrides por tenant** (delta sobre la base en código) — no se rediseña nada.

### S2
> "El punto es que un vendedor puede ser el mismo cajero ahora si va a existir el cajero solo debe tener acceso al pos"

Un usuario tiene **N roles**; sus permisos efectivos son la **unión** de los permisos de esos roles. El rol `Cajero` concede únicamente permisos del POS; el rol `Vendedor` concede únicamente los de ventas. La misma persona con ambos roles puede vender en back-office y operar la caja.

Esto evita la explosión combinatoria de roles (`VendedorCajero`, `CajeroAlmacenero`, …) que aparece en cuanto cada área nueva exige un rol propio.

### S3
> "si va a existir el cajero solo debe tener acceso al pos"

El cajero **no** puede leer el libro de ventas, ni el inventario completo, ni la contabilidad. Solo el POS. Y dentro del POS necesita leer el **catálogo de productos** (nombre, precio, stock) pero **no el costo** — el costo no se le expone en el DTO.

### S4
> "el cajero solo debe tener acceso al pos" (consecuencia técnica necesaria)

**Deny-by-default**: todo endpoint protegido declara un permiso explícito; lo que no declara, se denies. Hoy el modelo es el contrario: `SalesController` tiene `[Authorize]` de clase (`Volterp.Api/Controllers/SalesController.cs:14`) y solo el `DELETE` lleva roles (línea 106-107) → cualquier autenticado lee y crea ventas. Sin deny-by-default, "cajero = solo POS" es falso: el cajero igual vería el libro de ventas.

### S5
> "poner el pos aparte con un cajero, turno y eso"

El vocabulario de permisos es **módulo × acción de dominio**, no `read/write/delete` genéricos: `pos.read`, `pos.write`, `pos.void`, `pos.shift.open`, `pos.shift.close`, `pos.cash.movement`, `ventas.read`, `ventas.write`, `contabilidad.post`…

El catálogo es un **enum cerrado**: un permiso que no existe en el enum no se puede otorgar por error (el riesgo de una fila mal escrita en una tabla).

### S6
> "es la db que los contiene y los envia atraves de la api al front o como?"

El frontend **consume la lista de permisos de la API** (`GET /api/permissions/me` → lista plana de permisos efectivos, ya unida en el server) y no mantiene una matriz local. La unión de los roles se resuelve en el servidor.

### S7
> "tambien debe haber un registro de que se vendio, quien lo vendio etc"

La venta registra **quién vendió**, que es dato de dominio, distinto de `Sale.CreatedBy` (quién la registró, que es auditoría genérica). Si un supervisor registra una venta en nombre del cajero, `CreatedBy` miente.

### S8
> "el cajero de la manana entro con 5000 pesos, y vendo 5400 debe dejar la caja cuadrada con los 5000 y entregar el reporte de las ventas realizadas a su supervisor"

La caja se cuadra con: `Esperado = Fondo + Efectivo − Retiros + Entradas` · `Diferencia = Conteo − Esperado` · **caja cuadrada ⟺ diferencia = 0**.

### S9
Derivado de S8, y verificado en el código: **no existe modelo de pagos**. `Sale` solo tiene `Total`; no hay `PaymentMethod`, ni `SalePayment`, ni nada de efectivo en `Volterp.Domain`. Sin método de pago, "vendió 5,400" no dice cuánto entró a la caja y la aritmética de S8 no es demostrable.

### S10
*Recomendación (no pedida explícitamente)*: los nombres de rol no deben colisionar con los nombres de módulo. Hoy `UserRole.Ventas` (rol) y `ventas` (módulo) son la misma palabra, y esa colisión es la causa de la confusión entre "rol" y "área de trabajo". Renombrar `Ventas` → `Vendedor`, `Contabilidad` → `Contador`.

*Coste*: cambia el string del claim del JWT → hay que moverlo en lockstep con las constantes `Rol` del frontend y las sesiones vivas expiran. El `normalizeRole` del frontend hace fallback **silencioso** a `Ventas` ante un rol desconocido (`erp-mvp/src/domain/constants/roles.ts:49-55`): un rename mal sincronizado degrada permisos sin avisar.

### S11
Derivado de S3: la matriz no solo controla **endpoints**, también **qué campos salen en el DTO**. Un usuario sin permiso de costo no recibe `CostPrice` en la respuesta.

## Design

### El modelo mental

```
Rol      = paquete de permisos (función de la persona)   → código
Permiso  = módulo × acción de dominio                     → enum en código
Módulo   = área de trabajo (ventas, pos, inventario…)      → enum en código
Asignación usuario → roles                                 → base de datos
Permisos efectivos = unión de los roles del usuario        → calculada en el server
```

### Piezas nuevas

| Pieza | Ubicación | Rol |
|---|---|---|
| `Module` (enum) | `Volterp.Domain/Authorization/Module.cs` | Las áreas: `Sales`, `Purchases`, `Inventory`, `Clients`, `Suppliers`, `Accounting`, `Payroll`, `Employees`, `Reports`, `Administration`, `PointOfSale` |
| `Permission` (enum o record struct) | `Volterp.Domain/Authorization/Permission.cs` | `Module × Action`, clave textual `pos.void`, `ventas.read` |
| `RolePermissions` (static) | `Volterp.Domain/Authorization/RolePermissions.cs` | La matriz rol → permisos. Puro, sin dependencias |
| `IPermissionService` / `PermissionService` | `Volterp.Application/Interfaces/` + `Services/` | Resuelve permisos efectivos de un usuario/rol |
| `PermissionDto` | `Volterp.Application/DTOs/PermissionDtos.cs` | `MePermissionsDto(Role[], string[] Permissions)` |
| `PermissionRequirement` + handler | `Volterp.Api/Authorization/` | Lee el claim de rol → consulta la matriz → autoriza o no |
| `PermissionsController` | `Volterp.Api/Controllers/PermissionsController.cs` | `GET /api/permissions/me`, `GET /api/permissions/roles` (admin) |
| `UserRoles` (tabla puente) | nueva en `Volterp.Infrastructure` | `UserId` + `Role`, tenant-scoped |

### Enforcement

Cada endpoint pasa de `[Authorize(Roles = "Admin,SuperAdmin")]` a `[Authorize(Policy = "perm.ventas.delete")]`. Las policies se registran al boot (una por permiso conocido) y el handler resuelve contra `RolePermissions`.

`AuthorizationErrorMessages.cs:30` hoy lee `IAuthorizeData.Roles` para armar el mensaje del 403 → debe leer el permiso requerido en vez del rol, para que el mensaje siga siendo entendible bajo el modelo nuevo.

### Cambios de contrato (backend)

| Pieza | Hoy | Nuevo |
|---|---|---|
| `User.Role` | `UserRole` único (`Volterp.Domain/Entities/User.cs:17`) | colección de roles |
| `JwtService` | un claim `ClaimTypes.Role` (`Volterp.Infrastructure/Services/JwtService.cs:22`) | un claim por rol |
| `LoginResponse` | `Role` string (`Volterp.Application/DTOs/AuthDto.cs:4`) | `Roles` array |
| `UpdateUserRoleDto` | `UserRole Role` (reemplaza) | operación que **agrega y quita** roles |
| `BaseController.IsAdmin/IsSuperAdminOnly` | `FindFirst(ClaimTypes.Role)` (`Volterp.Api/Controllers/BaseController.cs:26-33`) | `IsInRole(...)` — con varios claims, `FindFirst` solo ve el primero |
| `CurrentUserService.IsSuperAdmin` | `FindFirst(ClaimTypes.Role)` (`Volterp.Api/Helpers/CurrentUserService.cs:12`) | `IsInRole(...)` |

### Cambios de contrato (frontend — repo `erp-mvp`, entrega aparte)

| Archivo | Cambio |
|---|---|
| `src/stores/authStore.ts:9` | `role: string` → `roles: string[]` |
| `src/hooks/usePermission.ts:22-28` | la unión de la matriz local desaparece: se consulta la lista de permisos que devuelve la API |
| `src/domain/constants/permisos.ts:20-99` | `MATRIZ_PERMISOS` se elimina (o queda solo como fallback hasta que la API exponga el catálogo) |
| `src/domain/constants/roles.ts:49-55` | `normalizeRole` deja de hacer fallback silencioso a `Ventas` ante un rol desconocido → falla cerrado |
| `src/components/Layout/Sidebar.tsx` | el filtrado de links pasa a los permisos del API |

### Cobertura de pruebas

| Prueba | Tipo | Qué fija |
|---|---|---|
| La matriz no otorga un permiso que no existe en el enum | unit | Catálogo cerrado |
| Los permisos efectivos de un usuario con N roles son la unión | unit | S2 |
| Un usuario con rol `Cajero` no puede leer `/api/sales` | integration | S3 + S4 |
| Un usuario con rol `Cajero` puede operar su turno | integration | S2 |
| Todo endpoint con `[Authorize]` declara un permiso | integration (inventario de endpoints) | **deny-by-default verificable** — sin este test, un endpoint nuevo queda abierto sin avisar |
| `GET /api/permissions/me` devuelve la lista plana | integration | S6 |
| Un token con dos claims de rol autoriza contra ambos | integration | S2 |
| Regresión: los endpoints que hoy solo piden `[Authorize]` siguen respondiendo igual a un usuario con rol `Ventas` | suite | Que el cambio no rompe el uso actual |

## Tasks

| ID | S# linked | Route | Status | Commit |
|---|---|---|---|---|
| T1 | S1, S5 | inline (parent) | PENDING | Catálogo `Module` + `Permission` + matriz `RolePermissions` en `Volterp.Domain/Authorization/` + tests |
| T2 | S2 | inline (parent) | PENDING | `User.Role` → colección: tabla puente `UserRoles` + migración + backfill + `UserService`/DTOs |
| T3 | S2 | inline (parent) | PENDING | JWT multi-claim + `LoginResponse.Roles[]` + `UpdateUserRoleDto` (agregar/quitar) + `IsInRole` en `BaseController` y `CurrentUserService` |
| T4 | S1, S4, S5 | inline (parent) | PENDING | `PermissionRequirement` + handler + policies registradas en `ServiceExtensions` + mensaje de 403 por permiso |
| T5 | S4 | inline (parent) | PENDING | Declarar permiso en **cada** endpoint (deny-by-default). Unit de trabajo por controller; el test de inventario de endpoints cierra la unidad |
| T6 | S6 | inline (parent) | PENDING | `PermissionsController` (`me`, `roles`) + `IPermissionService` + DTOs |
| T7 | S1-S6 | inline (parent) | PENDING | Verificación: suite completa + test de inventario de endpoints + E2E contra la API viva (login → `me` → un endpoint denegado → uno permitido) |
| T8 | S3, S6, S10 | handoff al usuario (repo `erp-mvp`) | PENDING | Front: `roles[]`, permisos desde la API, `MATRIZ_PERMISOS` fuera, `normalizeRole` falla cerrado. Entrega aparte (repo, rama y PR distintos) |
| T9 | S10 | inline (parent) | PENDING | Renombre de roles que colisionan con módulos (`Ventas`→`Vendedor`, `Contabilidad`→`Contador`) — en lockstep con T8 |
| T10 | S7, S8, S9 | inline (parent) | PENDING | **Feature dependiente**: formalizar el POS/caja como documento de feature aparte (ver apéndice). No se implementa dentro de esta feature |

## Risks

- **item 2 (seguridad)**: deny-by-default cambia el comportamiento de **todos** los endpoints. Implementado a medias, queda un endpoint sin permiso que sigue abierto para cualquier autenticado. Mitigación: el test de inventario de endpoints (T5) falla si un endpoint no declara permiso.
- **item 3 (contrato que otros consumen)**: `LoginResponse` cambia de `Role` a `Roles[]`; el frontend debe migrar en lockstep. Si el rename de T9 ocurre antes que T8, los tokens vivos llevan el rol viejo y `IsInRole` deja de matchear → sesiones abiertas empiezan a fallar con 401/403.
- **item 1 (datos)**: la migración de `User.Role` (único) a `UserRoles` reescribe filas. Requiere backfill determinista, orden de migración definido y verificación de que ningún usuario queda sin roles (un usuario sin roles = sin permisos = bloqueado).
- **item 5 (delivery)**: el frontend vive en otro repo (`erp-mvp`) y en la auditoría anterior tenía trabajo sin commitear del usuario → entrega aparte, sin tocar ese repo desde acá.
- **NO item 6**: la autorización actual sí tiene cobertura (`AuthorizationContractTests`, `JournalEntryEndpointIsolationTests`, `AccountEndpointIsolationTests`, `LoginAuthenticationTests`), pero **no hay ningún test que detecte un endpoint nuevo sin permiso** → ese test hay que escribirlo (T5).

## Apéndice — modelo de caja y turno (feature dependiente, no implementada aquí)

Se conserva el diseño acordado para que no se pierda. Se formaliza en su propio documento (T10).

### Entidades

| Entidad | Campos clave |
|---|---|
| `PointOfSale` (caja) | `CompanyId`, `Code`, `Name`, `IsActive` |
| `CashShift` (turno) | `PointOfSaleId`, `CashierUserId`, `Status`, `BusinessDate`, `Sequence`, `OpeningFloat`, `OpenedAt/By`, `ExpectedCash`, `CountedCash`, `Difference`, `DifferenceReason`, `HandoverAmount`, `ReceivedByUserId`, `ReceivedAt` |
| `SalePayment` | `SaleId`, `Method`, `Amount`, `Reference` |
| `CashMovement` | `ShiftId`, `Type` (Entrada/Retiro/Ajuste), `Amount`, `Reason` |
| `Sale` **gana** | `ShiftSessionId?`, `CashierUserId?`, `Channel` (Pos/BackOffice), `CostPrice` (snapshot), `VoidReason`/`VoidedBy`/`VoidedAt` |

`PaymentMethod` = `Cash | DebitCard | CreditCard | Transfer | StoreCredit`. El pago **mixto** es la norma en POS → `SalePayment` es entidad, no un enum en `Sale`.

### Aritmética (ejemplo del usuario)

```
APERTURA        fondo = 5,000
VENTAS          5,400  →  desglose por método: efectivo = 3,400 (si 2,000 fue tarjeta)
RETIROS         -1,000
ESPERADO        = 5,000 + 3,400 - 1,000 = 7,400
CONTEO FÍSICO   = 7,400
DIFERENCIA      = 0   ✅ caja cuadrada
ENTREGA         = 7,400 - 5,000 = 2,400   (el supervisor recibe las ventas; la caja queda con los 5,000)
```

### Invariantes

1. Una caja tiene **máximo un turno abierto** a la vez.
2. Un cajero no puede tener **dos turnos abiertos**.
3. **Toda venta en efectivo pertenece a un turno abierto** (si no, la aritmética deja de ser demostrable).
4. Ninguna venta se crea ni se modifica en un turno **cerrado**.
5. El conteo de un turno cerrado es **append-only**: una corrección se registra como un turno de ajuste, no editando el histórico.
6. `Diferencia ≠ 0` exige **motivo + validación de un supervisor**.
7. El `BusinessDate` del turno es el de la **apertura**; las ventas del turno heredan esa fecha (un turno que cruza medianoche no se parte en dos).
8. `HandoverAmount ≤ efectivo contado`.

### Estados del turno

`Open → Counted (el cajero cerró) → Reconciled (el supervisor validó y recibió)` + `Disputed` (diferencia rechazada).

### Permisos del POS

`pos.read` · `pos.write` · `pos.void` · `pos.shift.open` · `pos.shift.close` · `pos.shift.reopen` · `pos.cash.movement` · `pos.sale.discount` · `pos.report.read`

### Reporte de turno

`GET /api/pos/shifts/{id}/report`: ventas por método, cantidad de ventas, ticket promedio, efectivo esperado vs. contado, diferencia, retiros/entradas y el detalle de ventas.

### Bloqueo previo (fuera de esta feature)

**El stock se descuenta dos veces.** `CreateSaleAsync` descuenta (`Volterp.Application/Services/SaleService.cs:72`) y `CompleteSaleAsync` descuenta **otra vez** (línea 190); el propio comentario lo admite ("The double-deduction behavior (at creation AND completion) is preserved"). `Product.DeductStock` no tiene guarda contra la doble deducción (`Volterp.Domain/Entities/Product.cs:36-45`). Vende 2 unidades con stock 10 → quedan 6, no 8.

Es crítico para POS porque **toda venta de POS es create + complete**. Es un bugfix chico y aislado (con sus tests en `SaleServiceTests`) que hay que hacer **antes** de que el POS toque inventario.

## Log

### L1 — 2026-11-07
> "creemos un plan para luego ir a revisarlo y mejorar el sistema de roles, ponlo como pendiente de implementacion"

### L2 — 2026-11-07
> "champ validemos algo como se estan manejando los roles en la app es la db que los contiene y los envia atraves de la api al front o como?"

### L3 — 2026-11-07
> "Bueno como quedaria la matrix en la api porque no pasarlo a la db"

### L4 — 2026-11-07
> "bueno eso es un cambio de front la verdad por el momento que afecta a sale directamente"

### L5 — 2026-11-07
> "si pero si manana aparece otro modulo como el que quiero crear ahora separar Ventas del POS osea poner el pos aparte con un cajero, turno y eso"

### L6 — 2026-11-07
> "Eso es lo que pretendo planear porque debe ser interesante manejar toda esa data, por ejemplo el cajero de la manana entro con 5000 pesos, y vendo 5400 debe dejar la caja cuadrada con los 5000 y entregar el reporte de las ventas realizadas a su supervisor"

### L7 — 2026-11-07
> "tambien debe haber un registro de que se vendio, quien lo vendio etc"

### L8 — 2026-11-07
> "El punto es que un vendedor puede ser el mismo cajero ahora si va a existir el cajero solo debe tener acceso al pos"

### L9 — 2026-11-07 — decisiones de diseño (parent)

Exploración read-only del backend (`Volterp`) y del frontend (`erp-mvp`, repo aparte). Hallazgos que sostienen el plan:

- **La DB guarda el rol, no los permisos.** `Users.Role` es un enum persistido como **int** (`Volterp.Infrastructure/Migrations/20260924185219_InitialGuid7.cs:225`). El `HasMaxLength(30)` de `UserConfiguration.cs:20` sobre un int no hace nada. La matriz de permisos **no existe en el backend**: vive hardcodeada en el frontend (`erp-mvp/src/domain/constants/permisos.ts:20-99`).
- **El rol viaja por dos canales a la vez**: como claim `ClaimTypes.Role` del JWT (`JwtService.cs:22`) y como string en el body del login (`AuthDto.cs:4`, `AuthController.cs:56-57`). El frontend lo pasa a minúsculas en el login (`erp-mvp/src/pages/Login/Login.tsx:48`) y lo persiste en el store (`authStore.ts`).
- **El backend solo enforcea `Admin,SuperAdmin` en 24 endpoints** y el resto son `[Authorize]` de clase → cualquier autenticado lee todo el tenant. Los 24 guards están en: `AccountingTransactionsController`, `ClientsController`, `EmployeesController`, `PurchasesController`, `SalesController`, `SuppliersController`, `CompanyCertificatesController`, `AccountsController`, `JournalEntriesController`.
- **La matriz del front no gatea la UI**: `usePermission().canDelete` tiene **cero call sites**; los botones de borrar se renderizan según el `onDelete` que pase la página. El front muestra borrar y la API contesta 403.
- **`usePermission` deniega todo ante un rol desconocido** (`MATRIZ_PERMISOS[userRole]?.[modulo]?.[permiso] ?? false`), pero **`normalizeRole` degrada silenciosamente a `Ventas`** ante un rol desconocido (`erp-mvp/src/domain/constants/roles.ts:49-55`) → un rol nuevo del backend sería tratado como Ventas.
- **El front llama `/api/accounting-transactions` y la ruta real es `/api/AccountingTransactions`** (mismatch → 404), y **`/api/projects` no tiene controller**. Los módulos del front (`ventas`, `inventario`, `rrhh`, `proyectos`, `reportes`, `configuracion`) no mapean 1:1 con los recursos del backend → el catálogo de permisos debe ser propiedad de la API, no una copia del front.
- **Cambiar un rol en la DB no corta acceso**: el rol viaja en el JWT y **no existe endpoint de refresh** en `Volterp.Api`. Un usuario degradado conserva los permisos del token hasta que expira (`Jwt:ExpiryMinutes`) o cierra sesión.
- **El guard de asignación de roles ya existe y funciona**: solo SuperAdmin puede crear/asignar `Admin` o `SuperAdmin` (`UsersController.cs:39,92,121`) y `User.ChangeRole` prohíbe transiciones hacia/desde `SuperAdmin` para no-SuperAdmin (`Volterp.Domain/Entities/User.cs:25-37`).
- **`Sale.CreatedBy` sí guarda el UserId real** (`SaleService.cs:47` con `BaseController.GetCurrentUserId()`, `BaseController.cs:18-24`). Ojo: existe otro resolver, `ICurrentUserService.GetUserId()`, que devuelve `int?` con `id.GetHashCode()` (`CurrentUserService.cs:26-34`) — es un hash, no reversible, y no debe usarse para auditoría. Hoy no tiene call sites.
- **`Sale` no tiene método de pago** (S9), **ni canal**, **ni cajero**, **ni turno**. `Sale.Cancel()` solo funciona desde `Pending` (`Sale.cs:57-63`), y en POS la venta se cobra al instante → anular una venta cobrada es un caso nuevo de dominio (`Void` + motivo + actor).
- **`Sale.BusinessDate`** (`Sale.cs:19`) resuelve la fecha en la zona horaria del negocio → el turno debe abide a esa misma fecha, no a la del navegador.
- **La contabilidad ya tiene el patrón de "origen"**: `JournalEntry.Source`/`SourceId`/`SourceCode` + `JournalSource { Manual=1, Sale=2, Purchase=3 }`. El POS debería seguir ese camino (`Sale.Channel`) en vez de inventar otro mecanismo.

Decisiones tomadas:

- **Matriz en código, no en la DB** (S1). La DB guarda la asignación; la base en código + overrides por tenant si algún cliente lo pide.
- **Multi-rol con unión de permisos** (S2), no un rol por persona. Es lo único que no obliga a renombrar roles cada vez que aparece un área.
- **Deny-by-default** (S4): sin esto "cajero = solo POS" es falso.
- **Catálogo cerrado de permisos con acciones de dominio** (S5), no `read/write/delete` genéricos — el POS necesita distinguir "anular venta" de "cerrar turno" para poder auditar quién hizo qué.
- **El servidor calcula la unión y devuelve la lista plana** (S6): el frontend deja de mantener matriz.
- **El POS se formaliza como feature aparte** (T10); esta feature es solo el sistema de roles.

### L10 — 2026-11-07 — estado

Plan creado. **Nada implementado**: todas las tareas PENDING. El repositorio queda sin cambios de código; solo este documento.

### L11 — 2026-11-16 — el plan entra al repositorio

- **Qué**: el documento estaba **sin commitear** desde el 2026-11-07 → hoy se versiona ✓ (el plan es la especificación que los workers van a leer).
- **Corrección**: la línea de la rama decía `feature/accounting` (la rama activa en aquel momento) → la rama activa hoy es `feature/electronic-invoicing` ✓ → la feature se abre con su propia rama cuando el owner la active ✓.
- **Estado**: sigue siendo **PLAN** ✓ → **nada implementado** ✓ → las 10 tareas (T1-T10) siguen **PENDING** ✓ → el bloqueo previo del doble descuento de stock (apéndice) sigue abierto ✓.
