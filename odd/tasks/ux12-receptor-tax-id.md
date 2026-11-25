# UX12 — El RNC del comprador es de la venta

Feature: `ux12-receptor-tax-id` (unidad de `facturacion-ux`)
Rama: `feature/ux12-receptor-rnc` (base `development`)
Delivery: ask-on-risk
Test runner: `dotnet test Volterp.Tests/Volterp.Tests.csproj`
Patrón: `NET-API-SKELETON-TEMPLATE.md` — el código sigue ese contrato, sin desvíos.

Objetivo: que el **RNC del comprador sea un dato de la venta** y que el documento
**nombre a su comprador cuando la norma lo exige**, en el camino electrónico (E31/E32)
y en el de papel (B01/B02).

## Specs

### S1

> "si yo quiero facturar con comprobante fiscal uso un NCF si el clente no exite le pido su RNC y le hago la venta"

El RNC que el cajero pide en el mostrador **queda en la venta**: `CreateSaleRequest` y
`UpdateSaleRequest` llevan `ReceptorTaxId` y la venta lo guarda.

### S2

> "osea nuestro sistema debe hacer eso, pero valida todo"

Las reglas son de la DGII, no de memoria:

| Documento | RNC del comprador |
|---|---|
| **E31 / B01** (crédito fiscal) | **siempre obligatorio** (quien compra deduce el ITBIS) |
| **E32 / B02** (consumo) | obligatorio **desde RD$250,000**; por debajo el comprador es el consumidor final y el documento no lo nombra |

### S3

> "Exacto asi seria lo correcto a menos que el cliente quiera que lo registren"

El padrón de clientes **no** pisa el RNC que trae la venta: la ficha del cliente solo
rellena el hueco cuando la venta **no** trae uno. Y una venta que ya trae un RNC no lo
pierde porque una actualización venga sin él.

## Tasks

| ID | S# linked | Route | Status | Commit / evidencia |
|---|---|---|---|---|
| T1 | S1 | inline (parent) | DONE | `3183ea9` · `CreateSaleRequest`/`UpdateSaleRequest` llevan `ReceptorTaxId` (el contrato) · el `MapTo` de create **no** lo copia: el service es su dueño |
| T2 | S2, S3 | delegated (worker) + inline (parent) | DONE | `3183ea9` · `BuyerTaxIdPolicy` (nuevo) + el guard de `Sale.Complete()` + `SetReceptorTaxId` + la resolución en `SaleService` · RED observado antes (6 en rojo ✓) → VERDE (38/38 ✓) · el diff lo revisó el padre ✓ |
| T3 | S1-S3 | inline (parent) | DONE | `e76d798` (el trabajo previo de la sesión, unidad propia ✓) + `3183ea9` · suite completa **1141 verdes · 0 rojos · 9 saltados** (los que necesitan Postgres real ✓) + build sin warnings ✓ |
| T2b | S2 | inline (parent) | DONE | El **umbral viaja en el readiness** ✓ (`FiscalReadinessDto.BuyerTaxIdRequiredFrom` ✓) → el front pregunta "¿esta venta nombra al comprador?" con el número del back ✓ y no con uno escrito a mano ✓ |
| T4 | S1 | handoff (repo `erp-mvp`) | PENDING | **Fase 2, después del back**: la caja pide el RNC y lo deja en la venta |

## Log

### L1 — 2026-11-24 — el dueño abre la unidad

> (el dueño: *"Ok comencemos pero no hagas trabajo paralelo si es el backend primero y luego el front o viceversa"* · *"no hagas nada que no siga el patron del proyecto recuerdalo"*)

- **El orden** ✓: **nada de trabajo en paralelo** ✓ → el **back primero** ✓ (es el contrato) → el **front después** ✓ (fase 2, T4 ✓), cada uno en su repo y en su rama ✓.
- **El patrón manda** ✓: todo el código de esta unidad sigue `NET-API-SKELETON-TEMPLATE.md` ✓ (§4.9 el VO es la única fuente de verdad ✓ · §4.8 el pipeline Either ✓ · §2.1 el dominio es dueño de los invariantes ✓).
- **Las decisiones** ✓ (del plan, `odd/tasks/facturacion-ux.md` L10/L11):
  - El RNC del request se **valida y normaliza con el VO `Rnc`** (`Volterp.Domain/ValueObjects/Rnc.cs` ✓) → el `Ensure()` del service lo rechaza si no es un RNC o una cédula ✓ → se guarda la forma canónica (solo dígitos ✓).
  - El RNC de la ficha del cliente **sigue como está** ✓ (el `ClientService` ya lo valida y normaliza al guardarlo ✓ → no se toca el dato de las ventas existentes ✓).
  - La regla vive en el **dominio** (`Sale.Complete()` ✓) → el mismo camino para el e-CF y para el papel ✓ → **falla cerrado antes de firmar el e-CF o de quemar el número del NCF** ✓.
  - El umbral es el del RFCE (`RfceDocument.MaximumAmount` ✓) → una sola constante ✓.
  - La regla **no se aplica al guardar el borrador** ✓: la caja puede guardar el borrador y pedir el RNC al completar ✓ → lo que se rechaza es la **emisión** ✓.
  - **No hay migración** ✓: la columna `ReceptorTaxId` existe ✓ y `SaleDto` ya la expone ✓.
- **El mensaje del E31 no cambia** ✓ (contrato con el front ✓): *"Sale must have the receptor tax id to be completed as a fiscal credit."* ✓ → el de la consumo es nuevo ✓.
- **Riesgo** ✓: item 3 (contrato) — la API acepta un campo nuevo en `POST /api/sales` y en `PUT /api/sales/{id}` ✓ → **el back primero** ✓ → el front después ✓ · item 1 (datos) — el RNC queda en esa venta ✓ (no toca el padrón de clientes ✓).
- **Para la fase 2 (front)** ✓: la caja necesita el **umbral de RD$250,000** para avisar antes de firmar → el back es la fuente de verdad de la regla → lo natural es **exponerlo en el readiness** (`/api/ElectronicInvoices/readiness`) y que el front no lo duplique como número mágico ✓ → se decide al abrir la fase 2 ✓ (hoy el back solo lo usa para **rechazar** ✓).

### L2 — 2026-11-24 — T2: la regla del dominio y la resolución del RNC

- **La regla** ✓: un policy del dominio (`Volterp.Domain/Policies/BuyerTaxIdPolicy.cs` ✓) y `Sale.Complete()` la lee ✓ → el mismo camino para el e-CF y para el papel ✓ → **falla cerrado antes de firmar el e-CF o de quemar el número del NCF** ✓. El umbral es el del RFCE (`RfceDocument.MaximumAmount` ✓) → una sola constante ✓. Las notas (E33/E34) no lo exigen: toman el comprador del documento que modifican ✓.
- **El mensaje del E31 no cambió** ✓ (es contrato con el front ✓): *"Sale must have the receptor tax id to be completed as a fiscal credit."* ✓ → el de la consumo es nuevo ✓.
- **El RNC se valida con el VO** ✓ (`Rnc.TryParse` ✓ en el `Ensure` del service ✓ · §4.9 del template ✓) y se guarda en su forma canónica (solo dígitos ✓) → **el padrón de clientes no se toca** ✓ (el `ClientService` ya lo valida y normaliza ✓ → lo que llega de ahí ya es canónico ✓).
- **Quién gana** ✓: el RNC que viaja con la venta ✓ → la ficha del cliente solo rellena el hueco ✓ → y una actualización **sin** RNC **no borra** el que la venta ya tenía ✓.
- **Sin migración** ✓: la columna existe ✓ y `SaleDto` ya la expone ✓ → el front ya lo lee ✓.
- **La regla se aplica al completar** ✓ (no al guardar el borrador ✓): la caja puede guardar el borrador y pedir el RNC al completar ✓ → lo que se rechaza es la **emisión** ✓ → el front avisa antes (fase 2 ✓).
- **Un caso que el padre revisó** ✓: la factura comercial (sin documento fiscal) también pasa por `Complete()` ✓ → la regla le aplica igual que la del E31 ✓ (que ya era así ✓) → y el flujo real del front **no** cae ahí ✓ (completa con `asCommercialInvoice: false` ✓ → se emite documento ✓).
- **Verificación** ✓: el RED se vio antes de implementar ✓ (6 tests nuevos en rojo ✓) → VERDE ✓ (38/38 en las dos clases ✓) → el diff lo revisó el padre ✓ → suite completa ✓ → build ✓.
- **Risk**: none ✓ (el campo es nuevo y opcional ✓ · la regla nueva es la pedida ✓ · los datos existentes no se tocan ✓ · el seeder de demo crea la venta ya completada con `Total = 118` ✓ → no la afecta ✓).

### L3 — 2026-11-24 — los commits y la evidencia

- **`e76d798`** ✓: el trabajo que había quedado **sin commitear** de la sesión anterior (la lista de ventas por estado trae sus renglones ✓) → su propia unidad ✓ → va primero porque el árbol estaba sucio ✓.
- **`3183ea9`** ✓: UX12 del back ✓ → los DTOs ✓ · la resolución del RNC ✓ · la regla del dominio ✓ · los tests ✓ · el README ✓.
- **Evidencia** ✓: el RED se vio **antes** de implementar ✓ (6 tests nuevos en rojo ✓) → VERDE ✓ (38/38 en las dos clases ✓) → **suite completa: 1141 verdes · 0 rojos · 9 saltados** ✓ (los saltados son los que necesitan Postgres real ✓) → build sin warnings ✓ → el diff lo revisó el padre ✓.
- **Chequeo diferencial** ✓: el único camino que completa ventas es `SaleService.CompleteSaleAsync` ✓ → el seeder de demo crea la venta **ya completada** con `Total = 118` y sin RNC ✓ → no lo afecta ✓ → las ventas del seed viejo van por SQL ✓ (no pasan por el dominio ✓).
- **Risk**: none ✓.
- **Lo que sigue** ✓: la **fase 2** (front ✓) → la caja pide el RNC y lo deja en la venta ✓ → y la decisión del umbral (exponerlo en el readiness ✓ para que el front no duplique el número ✓).

### L4 — 2026-11-24 — el umbral viaja en el readiness

> (decisión del padre, para que la fase 2 no invente el número)

- **El front necesita el umbral** ✓: la caja tiene que avisar **antes de firmar** que una venta de RD$300,000 es nominativa y tiene que nombrar a su comprador ✓ → y ese número **no puede estar escrito a mano en el front** ✓ (la regla es del back ✓ → el back la dice ✓).
- **Dónde viaja** ✓: en el readiness (`GET /api/Sales/readiness` ✓ → `FiscalReadinessDto.BuyerTaxIdRequiredFrom` ✓) → que es donde el front ya pregunta si el contribuyente puede facturar ✓ → un solo lugar ✓ → una sola fuente ✓.
- **El valor** ✓: el del dominio (`BuyerTaxIdPolicy.NominativeFrom` ✓ = `RfceDocument.MaximumAmount` ✓) → ni el back ni el front lo duplican ✓.
- **Es un campo más de la respuesta** ✓ → no rompe a quien ya la consume ✓ (el front lo lee cuando está ✓).
- **Verificación** ✓: RED antes ✓ (`Expected 250000M, but found 0M` ✓) → VERDE ✓ → suite completa ✓.
- **Risk**: none ✓ (campo nuevo en una respuesta ✓ · la regla ya vive en el dominio ✓ · no cambia ninguna fila guardada ✓).
