# Sistema de roles y permisos — matriz en la API, roles múltiples, deny-by-default

Feature: `roles-permissions`
Branch: `feature/roles-permissions` (creada el 2026-11-19 desde `development` @ `7ac20a7`)
Delivery: `ask-on-risk` (default)
Test runner: `dotnet test Volterp.Tests/Volterp.Tests.csproj`

Objetivo: que el sistema de roles deje de ser un enum único con guards binarios `Admin/SuperAdmin` sobre endpoints sueltos, y pase a ser tres cosas: **roles múltiples por usuario**, **matriz rol→permisos en código dentro de la API**, y **cada endpoint declarando su permiso (deny-by-default)**.

Conductor: el módulo POS (cajero, turno, caja cuadrada) es lo que hace insuficiente el modelo actual. Un "vendedor" que también opera la caja no cabe en un `User.Role` único, y "el cajero solo accede al POS" es imposible de expresar mientras todo endpoint autenticado lea todo.

**Estado: EN EJECUCIÓN. T1 y T11 cerradas; T2-T10 siguen PENDING.**

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

**Deny-by-default**: todo endpoint protegido dice **qué permiso necesita**, y ese permiso lo **resuelve la matriz** según los roles del caller. Hoy el modelo es el contrario: `SalesController` tiene `[Authorize]` de clase (`Volterp.Api/Controllers/SalesController.cs:14`) y solo el `DELETE` lleva roles (línea 106-107) → cualquier autenticado lee y crea ventas. Sin deny-by-default, "cajero = solo POS" es falso: el cajero igual vería el libro de ventas.

**Dónde vive esa declaración (cambiado el 2026-11-20, L18)**: no en un atributo por endpoint, sino en **un mapa de rutas → permiso del API** (`Volterp.Api/Authorization/`), que un filtro consulta. Motivo (el owner): *"no lo veo viable"* declarar el permiso en cada endpoint → con el mapa, la **matriz sigue siendo la fuente de verdad** (rol → permisos ✓) y el diff es un archivo, no 24 controllers. La regla de deny-by-default no cambia: **una ruta que el mapa no cubre queda cerrada** ✓, y el **test de inventario** sigue siendo la red que falla si un endpoint no está cubierto.

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
| T1 | S1, S5 | inline (parent) | DONE | Catálogo `Module` + `Permission` + matriz `RolePermissions` en `Volterp.Domain/Authorization/` + tests — la matriz es **la del front** (`erp-mvp/src/domain/constants/permisos.ts`) y el vocabulario es el del plan (módulo × acción de dominio) |
| T2 | S2 | inline (parent) | DONE | `User.Role` → colección: tabla puente `UserRoles` (tenant-scoped, índice único `(UserId, Role)`) + migración `AddUserRoles` con backfill + drop de la columna + `User.AssignRole/RemoveRole/HasRole` + `UserService.UpdateRolesAsync` + `PUT /api/Users/{id}/roles`. **Se hizo junto con T3**: sacar `User.Role` de la persistencia obliga a cambiar el token en el mismo commit |
| T3 | S2 | inline (parent) | DONE | JWT multi-claim (un claim por rol) + `LoginResponse.Roles[]` + `CreateUserDto.Roles[]` + `UserDto.Roles[]` + `IsInRole` en `BaseController` y `CurrentUserService` (con varios claims, `FindFirst` solo veía el primero) |
| T4 | S1, S4, S5 | inline (parent) | DONE | `PermissionRequirement` + handler + una policy por permiso (`PermissionPolicies`, los nombres son constantes) + el 403 nombra el permiso que falta. La unión de los permisos de los roles vive en `RolePermissions.For(IEnumerable<UserRole>)` |
| T5 | S4 | inline (parent) | DONE | Deny-by-default con el permiso de cada ruta en **un mapa del API** (no un atributo por endpoint, L18) + el filtro que lo consulta contra la matriz + el test de inventario que exige que toda ruta JWT esté cubierta |
| T6 | S6 | inline (parent) | DONE | `PermissionsController` (`me`, `roles`) + `IPermissionService` + DTOs |
| T7 | S1-S6 | inline (parent) | PENDING | Verificación: suite completa + test de inventario de endpoints + E2E contra la API viva (login → `me` → un endpoint denegado → uno permitido) |
| T8 | S3, S6, S10 | handoff al usuario (repo `erp-mvp`) | PENDING | Front: `roles[]`, permisos desde la API, `MATRIZ_PERMISOS` fuera, `normalizeRole` falla cerrado. Entrega aparte (repo, rama y PR distintos) |
| T9 | S10 | inline (parent) | PENDING | Renombre de roles que colisionan con módulos (`Ventas`→`Vendedor`, `Contabilidad`→`Contador`) — en lockstep con T8 |
| T10 | S7, S8, S9 | inline (parent) | PENDING | **Feature dependiente**: formalizar el POS/caja como documento de feature aparte (ver apéndice). No se implementa dentro de esta feature |
| T11 | S4 | inline (parent) | DONE | `GET /api/ElectronicInvoices/{id}` — la lectura de un comprobante por id (la ruta que el front daba por existente y no existía). Vive en esta feature porque deny-by-default (T5) le tiene que declarar su permiso |

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

### L12 — 2026-11-19 — la feature se abre y el endpoint del front entra primero

> "champ como ya tenemos la facturacion electronica lista ahora debemos verificar para que todo quede alineado la matrix de permisos que dejamos pendientes trae a colacion eso y el plan que tenemos para implementarlo, tengo otro agente hologando el front encontro un hueco cone l endpoint electronic/invoicing/{id} validame eso por favor"

> "y pasemonos a development para crear un feature branch desde ahi para implementar las mejoras"

- **La rama**: `feature/roles-permissions` creada desde `development` @ `7ac20a7` ✓ (el plan vivía en el repo desde el 2026-11-07 sin rama propia ✓). **Ojo**: `feature/electronic-invoicing` conserva **2 commits sin pushear** que **no** están en `development` (`7310a6d` la limpieza de filas legacy que bloqueaba el borrado de la compañía mock ✓ · `fa2a874` el cierre de T52 ✓) → quedan en esa rama ✓ (nada se perdió ✓) → su merge a `development` es decisión del owner ✓.
- **El endpoint del front (`electronic/invoicing/{id}`)**: **no existe** ✓ — no hay tal ruta en ningún controller ✓ (todos usan `[Route("api/[controller]")]` ✓). Lo más cerca: `GET api/ElectronicInvoicePrint/{id}` (HTML imprimible ✓) · `GET api/ElectronicInvoices/{id}/qr.svg|.png` ✓ · `POST api/ElectronicInvoices/{id}/retry` ✓ · `GET api/ElectronicInvoices/state` ✓. **El hueco real**: **no existía ninguna lectura de un comprobante por id** ✓ → el front no tenía cómo traer el detalle de un comprobante ✓.
- **T11 (cerrada)**: `GET /api/ElectronicInvoices/{id}` ✓ → `IElectronicInvoiceService.GetByIdAsync` ✓ (`ErrorKind.NotFound` ✓ → 404 ✓) → el DTO `ElectronicInvoiceDto` que ya devolvía el retry ✓ (**sin cambiar el contrato de ese endpoint** ✓). El tenant sale del token ✓ y el documento de otro tenant es **404** ✓ (no 403 ✓). Tests: `ElectronicInvoiceGetByIdEndpointTests` (4 ✓: el documento del tenant ✓ · el de otro tenant es 404 ✓ · el que no existe es 404 ✓ · **la ruta literal `state` no se rompió** ✓ por el `{id}` nuevo ✓).
- **Por qué T11 entra en esta feature y no en la del e-CF**: deny-by-default (T5) le tiene que declarar su permiso ✓ → el endpoint tiene que existir antes de que el barrido le pase por encima ✓. Y desbloquea al agente del front ✓.
- **Verificación de la unidad**: build limpio ✓ (0 errores ✓) · el test enfocado **4/4** ✓ · la differential check de los endpoints hermanos del mismo controller: **260** de `Volterp.Tests` ✓ + **5** de `Volterp.ElectronicInvoicing.Tests` ✓, sin regresiones ✓.
- **Hallazgo abierto (no tocado)**: `ElectronicInvoiceDto.MapFrom` copia `VerdictNote` **sin pasar por `RawResponseSanitizer`** ✓ (el `ElectronicInvoiceStateDto` sí lo pasa ✓) → el `retry` ya expone la nota sin redactar ✓ y el `GET` nuevo hereda eso ✓. No se tocó porque `ElectronicInvoiceDto` lo consume el `retry` ✓ (cambiarlo es un cambio de contrato de un endpoint que este pedido no nombró ✓) → **decisión del owner**.

### L13 — 2026-11-19 — T1: el catálogo y la matriz (la matriz es la del front)

> "eso ya existe validalo en el codigo del backend o del frontend que es quien la gestiona por el momento"

> "El plan tambien te da algunas pautas a seguir"

- **La matriz ya existe** ✓ → la gestiona el front ✓ (`erp-mvp/src/domain/constants/permisos.ts` ✓): **12 módulos** × `read/write/delete` × **6 roles** ✓ (los mismos `UserRole` del backend ✓ · los valores 1..6 coinciden ✓).
- **Lo que se implementó** ✓ (T1 ✓):
  - `Volterp.Domain/Authorization/Module.cs` → los 13 módulos (los 12 del front ✓ + `PointOfSale` ✓ del plan ✓).
  - `Volterp.Domain/Authorization/Permission.cs` → **el enum cerrado** ✓ (46 permisos ✓): el `read/write/delete` del front ✓ + `contabilidad.post` ✓ + el set del POS ✓ (`pos.read`…`pos.report.read` ✓ · el del plan ✓).
  - `Volterp.Domain/Authorization/Permissions.cs` → el módulo y la clave de cada permiso ✓ — **el switch no tiene brazo `default`** ✓ → un permiso nuevo sin catalogar **no compila** ✓.
  - `Volterp.Domain/Authorization/RolePermissions.cs` → **la matriz** ✓: los mismos módulos, los mismos roles y los mismos grants del front ✓ (`SuperAdmin`/`Admin` = todo ✓ · `Ventas` ✓ · `Inventario` ✓ · `Contabilidad` ✓ · `RRHH` ✓).
- **Las pautas del plan que se respetaron** ✓: la matriz **en código** y no en la DB ✓ (S1 ✓) · el catálogo **cerrado** ✓ (S5 ✓) · el vocabulario **módulo × acción de dominio** ✓ (S5 ✓) · la unión de los permisos la resuelve el servidor ✓ (S6 ✓ → el front deja su copia en T8 ✓).
- **Las claves no cambian** ✓: las del front (`ventas.read` ✓ · `contabilidad.read` ✓ · `facturacion.read` ✓ · `rrhh.read` ✓ · `configuracion.read` ✓) → la migración del front (T8) no renombra nada ✓.
- **Tests** ✓ (`RolePermissionsTests` ✓ · 11 ✓): las claves son únicas ✓ · la clave empieza por la del módulo ✓ · las claves son las del front ✓ · Admin/SuperAdmin tienen todo ✓ · **ningún permiso queda sin rol** ✓ · **la matriz de cada rol es la del front** ✓ (la paridad queda clavada por test ✓).
- **Dos cosas que quedan anotadas** ✓: (a) el front da `delete` de `facturacion` al rol `Ventas` ✓ → se conserva ✓ aunque **no hay endpoint que borre un e-CF** ✓ → el permiso no abre nada ✓; (b) el front da `write` de `dashboard` a los roles no admin ✓ → se conserva ✓ aunque el `DashboardController` solo lee ✓ → lo define el barrido de T5 ✓.

### L14 — 2026-11-19 — el veredicto sale redactado (cerrado el hallazgo que quedó abierto)

- **El hallazgo** ✓: `ElectronicInvoiceDto.MapFrom` copiaba `VerdictNote` **sin pasar por `RawResponseSanitizer`** ✓ → el `retry` exponía la nota sin redactar ✓ · y `SaleDto.MapFrom` hacía lo mismo con `FiscalNote` ✓ (la venta lleva el veredicto de su comprobante ✓) → **dos caminos de fuga** ✓ (el `ElectronicInvoiceStateDto` sí redactaba ✓ → la incoherencia estaba en el DTO del comprobante ✓).
- **El arreglo** ✓: los dos DTO pasan por `RawResponseSanitizer.Redact` ✓ — el mismo sanitizador que ya usaba el DTO del estado ✓. El texto del veredicto **no cambia** ✓ (el operador sigue leyendo el por qué ✓) · lo que sale es `[redacted]` en lo que tiene forma de credencial ✓.
- **Contrato**: cambian las respuestas de los endpoints que devuelven la nota (el `retry` ✓ · la emisión ✓ · la venta ✓) → **solo cambia el texto que tenía forma de credencial** ✓ → el front sigue leyendo la nota ✓.
- **Tests** ✓: `ElectronicInvoiceDtoTests` (4 ✓: la nota se conserva ✓ · el bearer de la DGII no sale ✓ · el token de la DGII no sale ✓ · una nota sin credencial no se toca ✓) + `SaleDtoTests` (3 ✓: la nota fiscal se conserva ✓ · el token no sale ✓ · una venta sin comprobante no tiene nota ✓) → **16/16** en los DTOs ✓ y **390** de e-CF + ventas ✓ sin regresiones ✓.

### L16 — 2026-11-20 — T2+T3: el usuario tiene muchos roles (persistencia + token)

> "Champ vamos a continuar con la implementacion de la matrix de permisos que dejamos aye, tambien vamos a ver el front si necesita algo"

- **T2 y T3 se hicieron en una sola unidad.** El plan las tenía separadas, pero sacar `User.Role` de la persistencia **obliga** a cambiar el token en el mismo commit: con `User.Role` afuera el `JwtService` ya no tiene de dónde leer el rol, y con un token de un solo rol el usuario pierde la unión de sus permisos. → el plan se corrige y las dos filas quedan con el mismo commit.
- **El dominio** ✓: `User.Role` (un valor) → `ICollection<UserRoleAssignment> Roles` + la entidad `UserRoleAssignment` (`Id`, `UserId`, `Role`, `CompanyId` + auditoría) · `ChangeRole` (que **reemplazaba**) → `AssignRole` / `RemoveRole` / `HasRole` (una colección se le agrega y se le saca, no se reemplaza). Lo demás de la entidad no se tocó ✓ (email, activar/desactivar, auditoría).
- **Dos invariantes quedan en el dominio** ✓: un usuario inactivo no cambia de roles ✓ y **el último rol nunca se saca** ✓ (un usuario sin roles = sin permisos = bloqueado, y no hay forma de volver → `RemoveRole` tira `InvalidRoleTransitionException`).
- **La guarda de SuperAdmin se mudó a donde se sabe quién pregunta** ✓. El `ChangeRole` viejo tenía la regla ("nadie promueve ni degrada a SuperAdmin") que **nunca se ejecutaba en el camino real**: `UserService.UpdateAsync` asignaba `x.Role = request.Role` directo y se saltaba el método de dominio → era **código muerto de protección**. La protección que sí funcionaba estaba en el controller (`CanManageUser`) → ahora vive en los dos lugares que conocen al actor ✓ (`UserService.EnsureSuperAdminIsNotTouched` + `UsersController.CanManageRoles`) → **solo un SuperAdmin otorga o quita el rol SuperAdmin** ✓.
- **La tabla puente** ✓: `UserRoles` con `CompanyId` (el filtro de tenant lee la columna ✓ · la entidad implementa `ITenantScoped` ✓) + índice único `(UserId, Role)` ✓ + FK a `Users` con cascade ✓ y a `Companies` con restrict ✓.
- **La migración** `20261120134550_AddUserRoles` ✓: crea la tabla → **backfill** (`INSERT ... SELECT gen_random_uuid(), u."Id", u."Role", u."CompanyId", u."CreatedAt" FROM "Users" u`) → **borra la columna `Users.Role`** ✓, todo dentro de la transacción de la migración ✓. El `Down` la recrea nullable ✓, la puebla con el **rol más bajo** de cada usuario (`DISTINCT ON`) ✓ y la pasa a NOT NULL ✓ antes de tirar la tabla → el rollback devuelve la columna con un valor **válido** (inventar un rol sería peor).
- **El token** lleva un claim por rol ✓ → `BaseController.IsAdmin`/`IsSuperAdminOnly` y `CurrentUserService.IsSuperAdmin` pasaron de `FindFirst(ClaimTypes.Role)` (que con varios claims **solo ve el primero**) a `User.IsInRole(...)` ✓.
- **El contrato que cambia** ✓ (el front tiene que migrar en lockstep → es T8): `LoginResponse.Role` (string) → `LoginResponse.Roles` (string[]) · `CreateUserDto.Role` → `CreateUserDto.Roles[]` · `UserDto.Role` → `UserDto.Roles[]` · `PUT /api/Users/{id}/role` → `PUT /api/Users/{id}/roles` (el conjunto completo: idempotente, sin estado parcial) · `UpdateUserRoleDto` → `UpdateUserRolesDto`.
- **Un login sin roles es un 401** ✓: un token sin claims de rol no concede nada y el usuario quedaría bloqueado sin forma de volver → el login lo rechaza explícitamente ✓.
- **Verificación** ✓: build 0 errores ✓ · **suite completa 1065/1065** ✓ (**sin skips**: los tests con Postgres corrieron de verdad ✓) · el bloque nuevo: `UserBehaviorTests` (13 ✓) · `JwtServiceTests` (7 ✓) · `UserRolesEndpointTests` (7 ✓: el conjunto que se pide se aplica ✓ · el rol que ya tenía no se duplica ✓ · un admin no otorga SuperAdmin (403) ✓ · un admin no le quita SuperAdmin (403) ✓ · un no-admin no cambia los roles (403) ✓ · el conjunto vacío es 400 ✓ · el login trae todos los roles y el token los lleva ✓) · `UserRolesBackfillTests` (1 ✓ contra Postgres real: el usuario conserva su rol ✓ y la columna desaparece ✓) · `TheWholeChainAppliesOnPostgres` (1 ✓: la cadena entera aplica, vuelve atrás y se reaplica ✓).
- **Dos ajustes a tests de casa** (y el porqué) ✓: (1) `JwtTestHelper` traduce el nombre del rol a `UserRole` ✓ — los probes mintaban un rol **"Employee"** que no existe en el enum ✓ → con el catálogo cerrado, un nombre que no es un rol cae al rol `Ventas` ✓ (basta para los probes que necesitan "un caller que no es admin" ✓). (2) `TheWholeChainAppliesOnPostgres` compara las columnas **sin la posición física** ✓: Postgres siempre **agrega al final** la columna que se vuelve a crear ✓ → una columna que se saca del medio de una tabla vuelve última ✓ → la posición no es parte de la forma de la tabla ✓ (las columnas sí) ✓.
- **La revisión nativa (RDD) no corrió** ⚠: el binario `gentle-ai` package-local no está en la máquina (el owner lo había eliminado) → sin binario no hay revisión nativa ni push ✓. Queda para cuando el owner lo decida ✓.

**Risk**: item 1 (datos) — la migración reescribe filas (backfill) y borra la columna `Users.Role`. Verificado con el backfill contra Postgres real ✓ y con la cadena completa de migraciones (apply → rollback → re-apply) ✓.

### L17 — 2026-11-20 — T4: el permiso que pide el endpoint y el que el 403 nombra

> "Champ vamos a continuar con la implementacion de la matrix de permisos que dejamos aye"

- **El endpoint declara el permiso que necesita** ✓: `PermissionRequirement` (un permiso del catálogo) + `PermissionRequirementHandler` (lee los roles del token, une sus permisos y deja entrar si el pedido está entre ellos) ✓.
- **Una policy por permiso** ✓ (`PermissionPolicies`): los nombres son **constantes** (`perm.ventas.read`, …) → un endpoint que nombra una policy que no existe es un error del compilador, no un 500 en producción ✓. Las 46 policies se registran al boot desde el catálogo ✓ (`Permissions.All`) → el catálogo cerrado no puede quedar atrás ✓.
- **La unión vive en el dominio** ✓: `RolePermissions.For(IEnumerable<UserRole>)` → la misma regla que usará el `PermissionService` de T6 ✓ (una sola fuente para la unión ✓).
- **El 403 nombra el permiso que falta** ✓: *"You do not have the permission 'inventario.read'."* → un caller que no sabe cuál le falta no puede hacer nada con el rechazo ✓. El canal es `HttpContext.Items` (el `ForbiddenContext` de .NET 10 no expone el `AuthorizationFailure` ✓).
- **El contrato de la API no cambia todavía** ✓: las policies existen pero **ningún endpoint las declara** → el comportamiento es el de antes ✓ (eso es T5, que es donde cambia cada endpoint ✓).
- **Tests** ✓: `PermissionPolicyTests` (7 ✓: toda permission del catálogo tiene su policy ✓ · los nombres son únicos ✓ · la unión de los roles ✓ · el que tiene el rol entra ✓ · el que no, no ✓ · con dos roles tiene los permisos de ambos ✓ · sin roles no tiene ninguno ✓ · un claim de un rol que nadie conoce no concede nada ✓) · `PermissionPolicyEndpointTests` (5 ✓: entra ✓ · se lo rechaza ✓ · el rechazo nombra el permiso ✓ · con dos roles entra por los dos ✓ · sin token ni se pregunta ✓) → **suite completa 1078/1078** ✓.

**Risk**: item 2 (seguridad) — las policies son el mecanismo del deny-by-default, pero mientras ningún endpoint las declare el comportamiento no cambia → el riesgo real vive en T5, y el test de inventario de endpoints de T5 es lo que lo cierra ✓.

### L18 — 2026-11-20 — T5 cambia de forma: el permiso de cada ruta vive en un mapa, no en un atributo

> "pero porque declarar el permiso en cada endpoint no lo veo viable eso, para eso se creo la matriz y por eso se han creado roles para que por medio al role se habiliten las rutas que dicho role puede ver"

> "eso fue lo que estaba en el plan o no?"

> "Ok pues demos mambo"

- **El plan decía A** (atributo por endpoint) ✓ y en tres lugares: **S4** ("todo endpoint protegido declara un permiso explícito") · **Design → Enforcement** ("Cada endpoint pasa de `[Authorize(Roles = ...)]` a `[Authorize(Policy = ...)]`") · **T5** ("Declarar permiso en **cada** endpoint... Unit de trabajo por controller"). **Lo que el owner propone (mapa + filtro) no estaba en el plan** ✓: salió de su objeción ✓ y por eso se anota acá con sus palabras ✓.
- **El modelo del owner es el correcto** ✓ y no cambia: la **matriz** (rol → permisos) es la fuente de verdad ✓ · el token ya lleva los roles ✓ · **deny-by-default** se mantiene ✓ (una ruta sin cubrir queda cerrada ✓).
- **Lo que cambia es DÓNDE se declara** el permiso de cada ruta: de repetido en 24 controllers a **un solo archivo** (`Volterp.Api/Authorization/`) que un filtro consulta ✓ → el diff es un mapa + un filtro + un test, en vez de 24 controllers ✓.
- **Los `[Authorize(Roles = "...")]` repetidos dejan de ser el enforcer** ✓ (el filtro pasa a ser el único que autoriza) → y el 403 **nombra el permiso** que falta ✓ (lo que T4 ya dejó listo ✓).
- **El filtro no toca** los endpoints públicos ni los que autentican con **API key** (los de la DGII) ✓ → esos quedan fuera del mapa y el test de inventario los lista como exentos ✓.

**Risk**: item 2 (seguridad) — cambiar quién autoriza es el cambio de comportamiento más grande de la feature: mitiga con el **test de inventario** (toda ruta JWT cubierta ✓) + la regresión de la suite completa ✓ + la revisión independiente (item 2 exige verificador) ✓.

### L19 — 2026-11-20 — T5: el deny-by-default con el permiso de cada ruta en un mapa

- **El mapa** ✓ (`Volterp.Api/Authorization/PermissionRoutes.cs`): qué necesita cada ruta, por **controller + método** (`Sales:GET` → `ventas.read`) → no por plantilla de ruta (la colisión de `api/Sales/pending` con `api/Sales/{id}` no existe así ✓) · 92 rutas, **84 de controller + 3 minimal APIs** ✓.
- **El filtro** ✓ (`PermissionAuthorizationFilter`): pide el permiso al mapa y lo resuelve contra la **matriz** ✓ → el 403 **nombra el permiso** que falta ✓. **Ningun controller fue tocado** ✓ (el diff son 2 archivos nuevos + el registro del filtro + 1 línea en `Program.cs`).
- **Deny-by-default** ✓: una ruta que el mapa no cubre **queda cerrada** ✓, y el **test de inventario** falla si una ruta JWT no está en el mapa ✓ (`EndpointPermissionInventoryTests` ✓).
- **Los `[Authorize(Roles = ...)]` repetidos se quedan** ✓: son la **barra más alta** que la matriz (borrar clientes/empleados/ventas/rangos/certificados es del SuperAdmin ✓) → **nada se relaja** ✓. El mapa pone el piso, el atributo lo sube ✓.
- **La matriz dejó de mentir** ✓: le daba a `Ventas` `clientes.write`, `facturacion.write` y `facturacion.delete`, y la API nunca lo dejó (403 ✓) → un vendedor podía **crear emisores y borrar certificados** si la matriz pasaba a mandar ✓ → la matriz ahora dice lo que la API hace ✓ (`Ventas` conserva `facturacion.read` ✓). El front tiene que dejar de ofrecer esos botones (T8) ✓.
- **La API alquilada no se toca** ✓: los callers que entran con **API key** no son el usuario (su principal no lleva roles ✓) → el filtro los deja pasar ✓ → los clientes que alquilan la API siguen como estaban ✓.
- **Las rutas públicas y las de la DGII** quedan fuera ✓ (el login ✓ · la semilla ✓ · la validación de certificado ✓ · la recepción con certificado ✓).
- **La única ruta que declara su permiso** es `GET /_diagnostics/exceptions` ✓ (una minimal API, no un controller: el filtro de MVC no la ve ✓ → lleva `RequireAuthorization(PermissionPolicies.AdministrationRead)` ✓).
- **Lo que el inventario corrigió** ✓: el mapa nombra a `Accounts` (que **no existe en esta rama** ✓ → vive en `feature/accounting` ✓) → esa entrada se sacó ✓ · la aprobación comercial de un comprobante recibido es la única ruta JWT de `ElectronicInvoiceReception` ✓ → `facturacion.write` ✓ · **`DELETE api/Sales/{id}` hoy no exige roles** ✓ (el tracker y S4 estaban desactualizados ✓) → con el mapa necesita `ventas.delete` (los admins ✓) → un **endurecimiento** ✓.
- **Endurecimientos** ✓ (y son el punto del deny-by-default): los roles que la matriz nohers concede pierden el acceso que tenían (hoy **cualquier** autenticado leía y escribía ventas ✓) → la API ahora coincide con lo que el front ya mostraba ✓ (el front escondía esos módulos ✓).
- **Tests** ✓: `EndpointPermissionInventoryTests` (3 ✓: toda ruta JWT está en el mapa ✓ · el mapa solo habla el catálogo ✓ · el mapa no nombra controllers inexistentes ✓) · `PermissionFilterTests` (6 ✓: el que tiene el rol entra ✓ · el que no, no ✓ · el rechazo nombra el permiso ✓ · con dos roles entra por los dos ✓ · el SuperAdmin entra por todo ✓ · el login es público ✓) · `RolePermissionsTests` (la matriz corregida ✓) → **suite completa 1087/1087** ✓.

**Risk**: item 2 (seguridad) — el cambio de quién autoriza es el más grande de la feature: mitigado con el test de inventario ✓ + la suite completa ✓ + la revisión independiente ✓. No hay relajación: la matriz se corrigió para decir lo que la API ya hacía ✓.

### L20 — 2026-11-20 — T6: los permisos del caller salen de la API

- **`GET /api/permissions/me`** ✓ → los roles que tiene y la **unión** de sus permisos (la lista plana que el front ya conoce ✓) → el front deja de mantener su matriz (S6 ✓).
- **`GET /api/permissions/roles`** ✓ → la matriz completa (qué otorga cada rol del catálogo ✓) → **solo los admins** ✓ (`configuracion.read` ✓).
- **`IPermissionService` + `PermissionService`** ✓ → la unión sale del **dominio** (`RolePermissions.For`) ✓ → el filtro y el servicio responden lo mismo ✓ (una sola fuente ✓).
- **La unión vive en un solo lugar** ✓: `CallerRoles` (los roles del token) lo usan el filtro y el controller ✓.
- **El mapa tiene un tercer estado** ✓: una ruta puede pedir **un permiso**, pedir **solo un token**, o **no estar en el mapa** (cerrada ✓). La ruta que responde *qué puede hacer el caller* no puede pedir un permiso para que se le pregunte ✓ → `Permissions.GetMe` ✓.
- **Tests** ✓: `PermissionServiceTests` (5 ✓: la unión ✓ · sin roles no hay permisos ✓ · los permisos de un rol son los de la matriz ✓ · el admin tiene todo ✓ · la matriz cubre todos los roles ✓) · `PermissionsEndpointTests` (5 ✓: el caller aprende lo que puede ✓ · con dos roles, la unión ✓ · la matriz es de los admins ✓ · un no-admin no la ve (403) ✓ · sin token no se pregunta ✓) → **suite completa 1097/1097** ✓.

**Risk**: item 2 (seguridad) — el endpoint expone los permisos del propio caller (no los de otro ✓) y la matriz completa es de los admins ✓.
