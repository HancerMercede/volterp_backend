# Facturación electrónica: la pantalla que el usuario entiende

Feature: `facturacion-ux`
Rama: `development` (el front vive en `erp-mvp`, repo aparte: entrega con rama y PR distintos)
Delivery: ask-on-risk
Test runner: `dotnet test Volterp.Tests/Volterp.Tests.csproj`

Objetivo: que la pantalla de facturación electrónica y la pantalla de nueva venta sean.intuitivas para quien vende, y que el usuario sepa **qué documento está sacando, en qué estado está ante la DGII y qué falta para poder facturar** — sin salir de la pantalla en la que está.

## Specs

### S1

> "tenemos la pantalla de invoicing donde ahi basicamente hacemos todo lo de la facturacion electronica pero me gustaria que sea mas user friendly intuitivo para el usuario"

La pantalla de facturación (`/facturacion`, hoy un hub de seis tarjetas: emisores, rangos, certificados, comprobantes, recepción, configuración) es **administración**, no operación. El usuario que vende no la necesita; el que configura sí. La operación vive en la venta.

### S2

> "validemos como la hace alegra y esos sistemas que ya tienen una posicion en el mercado"

La validación se hizo contra Alegra (POS y facturación, DR), DGII (Facturador Gratuito, Guía 6, modelos de Representación Impresa), Adm Cloud, Digisoft, Kuadre, Odoo (MX/PE) y QuickBooks. Los hallazgos están en el Log (L2) con su fuente.

### S3

> "en ventas tenemos la parte de nueva venta desde ahi el POS es que vendemos por eso creo que ahi es que debemos prender o apagar la facturacion electronica"

El POS es la pantalla de nueva venta: `/ventas` monta `NuevaVenta` como overlay (`erp-mvp/src/pages/Ventas/Ventas.tsx:87-94`) y `src/pages/POS/` es un **directorio vacío sin ruta** (código muerto).

La intuición es correcta en la mitad y hay que corregirla en la otra mitad (S5): el lugar donde se decide el documento **es** la caja, pero lo que se prende y apaga no es la factura de cada venta.

### S4

> "pero debemos hacer un plan para hacer esas mejoras"

Este documento es el plan: specs, hallazgos con evidencia, tareas con ruta y el bloqueo previo. **No se implementa nada de esta feature hasta que el owner lo abra.**

### S5

*Derivada de S3 y verificada en el código*: el interruptor de la facturación electrónica **no es por venta, es a nivel de empresa**, y es una **declaración ante la DGII**.

- El modo vive en `CompanyEmissionMode` (`Electronic` | `Normal`) y el back lo consulta al completar la venta (`Volterp.Application/Services/SaleService.cs:238`, `CompanyEmissionPolicy.UsesElectronicEmission`).
- Un contribuyente en modo electrónico **no puede** vender sin documento: el back lo rechaza con *"The commercial invoice is only for the taxpayer who invoices in the normal mode."* (`SaleService.cs:267`).
- En modo normal, la venta puede salir como **factura comercial** (sin NCF, sin documento fiscal, sin tocar ningún rango): es el parámetro `asCommercialInvoice` de `PUT /api/Sales/{id}/complete` (`SaleService.cs:191`).

Consecuencia para la UX: en la caja hay que poder elegir **el documento dentro del modo declarado** (consumo E32 / crédito fiscal E31 / comercial cuando el modo es normal), y el modo no se puede cambiar por venta. Mostrar un interruptor "facturación electrónica" por venta sería ** untrue**: el back lo rechazaría.

### S6

*Derivada de S1 y verificada en el código*: **hoy el POS no completa ninguna venta**, y por lo tanto no emite ningún comprobante.

- El front crea la venta con `POST /api/sales` (`erp-mvp/src/pages/Ventas/components/NuevaVenta.tsx:156-164`, `saleService.ts:55-65`) y **nunca** llama a `PUT /api/sales/{id}/complete`: la función `completeVenta` existe en el store (`ventaStore.ts:139-155`) y **no tiene ningún llamador**.
- El botón "Completar Venta" no completa: manda el estado del `<select>` (`NuevaVenta.tsx:224-228`), cuyo default es `pendiente` (`NuevaVenta.tsx:56-58`).
- Si el `<select>` está en "completada", la venta se **crea** ya completada (`POST /api/sales` con `status: "Completed"`), y la creación **no emite nada**: la emisión vive únicamente en `CompleteSaleAsync`. Resultado: venta completada sin documento fiscal.
- `UpdateSaleAsync` además rechaza `Status == Completed` con *"A sale can only be completed through the completion operation."* (`SaleService.cs:110`).

### S7

*Derivada de S1 y verificada en el código*: el estado fiscal de la venta se muestra **crudo y sin salida**: la lista de ventas muestra un chip con el enum `fiscalState` (`erp-mvp/src/pages/Ventas/components/VentasList.tsx:119-133`), el veredicto de la DGII vive solo en el módulo de e-CF (`FacturacionElectronica/Invoices/Invoices.tsx:202-205`) y no hay QR desde la venta (el QR solo existe en el módulo, `Invoices.tsx:300-320`).

### S8

*Derivada de S2*: el vocabulario que ve el usuario debe ser el de la DGII y estar **separado del estado de pago**: `Aceptado` / `Rechazado` / `Aceptado condicional` / `En proceso` / `Anulado` (DGII, Guía 6), con color (Alegra: verde = emitida, gris = en proceso) y sin el "por emitir" mezclando "no enviado" con "rechazado" (anti-patrón verificado en Alegra).

### S9

*Derivada de S1 y S2*: antes de vender hay que poder ver **si la empresa puede emitir**: certificado válido, rango con cupo, modo declarado, contingencia activa. Hoy el interruptor de modo (`Settings.tsx:715-780`) no avisa que falta el certificado ni que el rango se quedó sin números (anti-patrón verificado en Alegra: el "por falta de numeración" no se recupera solo).

### S10

*Derivada de la feature de roles (T8)*: el front hoy gatea la UI con su matriz local, que le da `facturacion.write` al rol `Ventas` (`erp-mvp/src/domain/constants/permisos.ts:65`) mientras el back solo deja escribir a Admin/SuperAdmin → **botones visibles que contestan 403**. Cuando la API exponga los permisos (T6/T8 de `roles-permissions`), el front los consume y desaparece la contradicción.

### S11

> "por eso el sistema es modular si quieres la facturacion electronica se te habilita sino sigues facturando normal"

La facturación electrónica es un **módulo opt-in**: el sistema nace pensado para ella (una empresa nueva nace en modo `Electronic`), pero el contribuyente que no la quiere **factura normal con el mismo sistema** (NCF en papel). El modo declarado **no es un defecto**: es la decisión del contribuyente ante la DGII.

Consecuencia para el readiness (UX6): lo que la pantalla muestra es **"el camino que la venta toma hoy"**, no "¿tienes el módulo electrónico?":
- En modo `Electronic` y sin contingencia → el camino es el **electrónico**: hacen falta **certificado vigente** y **rango de e-NCF con cupo** (E31/E32/E33/E34).
- En modo `Normal` **o dentro de una contingencia** → el camino es el **papel**: hace falta el **rango de NCF con cupo** (B01/B02/B03/B04) y **no hace falta certificado**.
- El modo **no es un bloqueo**: facturar normal es un derecho, no una falla.

### S12

> "osea el sistema ya de por si esta pensado para la FE, pero tambien si el cliente no desea FE puede facturar normal con el mismo sistema, es a eso que me refiero por eso nos NCF deben tambien usarse normal los b02, b01 etc"

El camino del NCF tiene que estar completo: los tipos **B01** (Factura de Crédito Fiscal) y **B02** (Factura de Consumo) son los que el contribuyente que no hace FE usa todos los días, y son los que la DGII pide **reportar cada mes**. El detalle de ese reporte está en `odd/tasks/reportes-dgii.md`.

### S13

> "si yo quiero facturar con comprobante fiscal uso un NCF si el clente no exite le pido su RNC y le hago la venta"

> "osea nuestro sistema debe hacer eso, pero valida todo"

> "Exacto asi seria lo correcto a menos que el cliente quiera que lo registren"

El RNC del comprador es un dato **de la venta**, no un dato del padrón de clientes: hoy `SaleService.ResolveReceptorTaxIdAsync` lo saca de la ficha del cliente (`sale.ReceptorTaxId = client?.TaxId`) → si el cliente no está registrado, la caja **no tiene dónde anotar** el RNC que le piden. La venta lleva su propio RNC; el padrón de clientes solo se toca cuando **el cliente quiere quedar registrado**.

**Las reglas que hay que validar (contra la DGII, no de memoria)**:

| Documento | RNC del comprador |
|---|---|
| **E31 / B01** (crédito fiscal) | **siempre obligatorio** (quien compra deduce el ITBIS) |
| **E32** (consumo electrónico) | **obligatorio desde RD$250,000**; por debajo el comprador es **opcional** (consumidor final: la sección Comprador se omite) |
| **B02** (consumo en papel) | **no hace falta** (es el comprobante del consumidor final) |

**Lo que el sistema ya hace** ✓: el E31 sin RNC se rechaza (`SaleEcfDocumentMapper.MapBuyer` → `profile.RequiresBuyerTaxId`, del `minOccurs` del XSD oficial) ✓ · el E32 sin RNC se emite sin la sección Comprador ✓ · el sistema ya decide por monto (`ElectronicInvoiceService.VerificationUrlOf` → el timbre del resumen del RFCE bajo RD$250,000, el del host del contribuyente de 250 mil para arriba) ✓ · el NCF de papel no exige receptor ✓.

**Lo que falta** ✗: (1) el E32 **de RD$250,000 para arriba sin RNC** no se rechaza en la caja (la DGII lo rechaza al enviarlo) ✗ · (2) el **B01 sin RNC** no se valida en el camino de papel ✗ · (3) la venta **no tiene campo** para el RNC que llega del mostrador ✗ · (4) el **RFCE** (T29c) no se envía al host de consumo → el E32 bajo RD$250,000 sigue por el camino del e-CF y su timbre no resuelve en la DGII ✗.

## Validación de mercado (L2)

| Sistema | Dónde está el interruptor | Flujo en la caja | Estados que ve el usuario |
|---|---|---|---|
| **Alegra** (DR) | No hay switch global: es la **numeración** (Serie E e-CF / Serie B NCF) elegida **en la caja, por transacción** | ítems → numeración → vender → método de pago → **Emitir** | `Estado` (Por cobrar/Cobrada) **separado** de `Estado de emisión` (Emitida/En proceso/Rechazada/Por emitir/No electrónica) |
| **DGII Facturador Gratuito** | — | borrador de RI + XML → **Firmar y Enviar** → pantalla de estado | Aceptado / Rechazado / Aceptado condicional / En proceso / Anulado |
| **Adm Cloud** | `Habilitada` Sí/No **por empresa** + paso aparte de firma | guardar → *Operaciones fiscales* → *Firmado electrónico* | En espera / Aprobada / Rechazada (no distingue "aceptado condicional") |
| **Digisoft** | e-CF **se pide por venta** | "si el cliente pide comprobante fiscal, se emite" | Estado DGII (ACEPTADO / ACEPTADO_COND) |
| **Odoo** | por diario y por método de pago | el ticket **no** es el CFDI: la factura es otro paso | To be Sent / Sent / To Cancel / Cancelled |
| **QuickBooks** | no hay capa fiscal DR | Invoice vs Sales receipt por momento del cobro | — |

**Patrones a copiar**

1. **La numeración es el interruptor** y se nombra en el lenguaje del usuario (Serie E vs Serie B), no "activar facturación electrónica" (Alegra).
2. **El tipo de documento se preselecciona** según el cliente (E31 exige RNC → E32 para Consumidor Final) para que el cajero no elija (Alegra POS).
3. **Estado de pago y estado fiscal en columnas separadas** (Alegra).
4. **Chips de estado con color y su significado explicado** (Alegra POS).
5. **Aviso proactivo** "tenés comprobantes pendientes" en vez de esperar que el usuario audite una lista (Alegra POS).
6. **Detectar la caída de la DGII y degrading avisando pero sin dejar de vender** (Adm Cloud).
7. **Camino offline sancionado con numeración propia** y estado de sincronización por documento (Alegra POS + contingencia del Decreto 587-24 en `docs/Informe-Tecnico-eCF-v1.0.txt` §19).
8. **QR, código de seguridad y fecha de firma** como parte de primera clase del documento impreso (DGII, Representación Impresa).
9. **"Facturar después" como acción de lote** (Adm Cloud: *Pendientes de Firmar*; Odoo: factura global por periodicidad).
10. **Autocompletar "Consumidor Final"** para que el camino B2C no tenga fricción (Alegra).

**Anti-patrones a no copiar**

1. **Alegrar al cajero con "Por emitir"**: arreglar un documento exige salir del POS → Contabilidad → filtrar → Emitir (5 clics).
2. **"Por emitir" mezcla dos causas**: no enviado vs rechazado por la DGII — solo el modelo colombiano documenta la causa.
3. **Adm Cloud separa crear de firmar**: un paso extra, con permisos, en el momento de cobrar.
4. **La numeración agotada es una trampa silenciosa**: sin aviso previo.
5. **El vocabulario de estado más pobre que el de la DGII** (Adm Cloud no distingue "aceptado condicional").
6. **No poder desactivar la facturación electrónica después deSigned un documento** (Adm Cloud): el front tiene que advertirlo antes, no después.
7. **Kuadre y Digisoft no documentan nada**: no hay referencia de UX que copiar.

**Regla de la DGII que el front debe conocer**

E32 (consumo) por debajo de RD$250,000 puede ser en papel continuo; **desde RD$250,000 es nominativa** (Representación Impresa, modelos ilustrativos). Es la regla que decide si el cajero necesita el RNC del cliente.

## Hallazgos con evidencia

| # | Hallazgo | Evidencia |
|---|---|---|
| F1 | El POS es un overlay de `/ventas`; `src/pages/POS/` está vacío | `erp-mvp/src/pages/Ventas/Ventas.tsx:87-94` · `erp-mvp/src/pages/POS/` (sin archivos) |
| F2 | El front nunca envía la elección fiscal (`asCommercialInvoice` = 0 hits) y nunca llama a `/complete` | `erp-mvp/src/infrastructure/api/saleService.ts:55-86` · `erp-mvp/src/stores/ventaStore.ts:139-155` (sin llamador) |
| F3 | El back sí soporta la elección por venta, y **solo en modo normal** | `Volterp.Application/Services/SaleService.cs:191,238,267` · `CompleteSaleRequest.cs:3` |
| F4 | El botón "Completar Venta" depende de un `<select>` y su default es pendiente | `erp-mvp/src/pages/Ventas/components/NuevaVenta.tsx:56-58,224-228` |
| F5 | Crear la venta con `status: Completed` deja la venta completada **sin documento** (la emisión solo existe en el complete) | `SaleService.cs:46-100` (create, sin emisión) vs `SaleService.cs:191+` |
| F6 | El back **recalcula** los totales (`sale.CalculateTotals()`), así que el ITBIS que muestra el front no es el que guarda: es desincronización de pantalla, no dato corrupto | `SaleService.cs:86` vs `erp-mvp/src/pages/Ventas/components/NuevaVenta.tsx:62-70,139` |
| F7 | El estado fiscal se ve crudo en la venta y el veredicto solo en el módulo de e-CF; no hay QR desde la venta | `erp-mvp/src/pages/Ventas/components/VentasList.tsx:119-133` · `FacturacionElectronica/Invoices/Invoices.tsx:202-205,300-320` |
| F8 | El interruptor de modo existe en Configuración, sin aviso de que falte el certificado o el rango | `erp-mvp/src/pages/FacturacionElectronica/Settings/Settings.tsx:715-780` |
| F9 | El front da `facturacion.write` a Ventas y el back solo deja escribir a Admin/SuperAdmin | `erp-mvp/src/domain/constants/permisos.ts:65` · `Volterp.Api/Controllers/CompanyIssuerController.cs` (guards Admin) |
| F10 | **El POS no completa ventas → el doble descuento de stock no se ve.** El bug está vivo: se descuenta en la creación y **otra vez** en lacompletion | `SaleService.cs:72` (create) y `SaleService.cs:203` (complete) · el propio comentario lo admite (`SaleService.cs:70`) |
| F11 | El front tiene un stepper (RRHH), toasts, modales y formularios de sección: los patrones que hacen falta ya existen | `erp-mvp/src/components/RRHH/EmpleadoForm.tsx:66-79` · `erp-mvp/src/stores/uiStore.ts:56-66` · `erp-mvp/src/hooks/useCrudForm.ts:6-65` |
| F12 | La i18n solo tiene `es`/`en` y el test de claves cubre `facturacion.*`, no `ventas.*` | `erp-mvp/src/test/pages/facturacionI18nKeys.test.ts:23-33` |

## Tasks

| ID | S# linked | Route | Status | Commit |
|---|---|---|---|---|
| UX0 | S6, F10 | inline (parent) | DONE | **Bloqueo previo**: el stock se descuenta dos veces (create + complete). Hoy el POS no completa y por eso no se nota; en cuanto UX1 conecte el complete, toda venta del POS descuenta el doble. Bugfix aislado con sus tests en `SaleServiceTests` |
| UX1 | S6, F4, F5 | handoff al front (`erp-mvp`) | DONE | El botón "Completar Venta" completa la venta: se llama `PUT /api/sales/{id}/complete` y el `<select>` de estado sale de la caja (o pasa a ser el tipo de documento) — commit `erp-mvp 7bb3ba6` (`completeVenta` desde `NuevaVenta.tsx:184,206`; `saleService.ts:79-81`) |
| UX2 | S5, F2, F3 | handoff al front (`erp-mvp`) | DONE | La elección del documento en la caja: **Consumo (E32)** / **Crédito fiscal (E31)** / **Comercial** (solo si el modo de la empresa es normal; el back rechaza el resto). Default por tipo de cliente |
| UX3 | S7, S8, F7 | handoff al front (`erp-mvp`) | DONE | El estado fiscal de la venta con el vocabulario de la DGII, con color, **separado del estado de pago**, con el veredicto y el QR de la venta |
| UX4 | S7, F7 | handoff al front (`erp-mvp`) | DONE | Un comprobante pendiente o rechazado se resuelve **desde la venta** (reintentar), sin el desvío de 5 clics a Contabilidad |
| UX5 | S9, F8 | handoff al front (`erp-mvp`) | PENDING | "Listo para facturar": checklist de certificado válido, rango con cupo, modo declarado y contingencia activa, en Configuración y en el punto de venta — **depende de UX6** (el checklist se alimenta del endpoint de readiness) |
| UX6 | S5, S9, S11 | inline (parent) | DONE | El back expone el estado de Readiness del **camino que la venta toma hoy**: modo, certificado, rango con cupo (e-NCF en modo electrónico, NCF en modo normal o en contingencia), contingencia, `IsReady` + bloqueos → `GET /api/Sales/readiness` (commits `a0e6507` y `72165a3`: la ruta vive con las ventas porque la usa el cajero ✓) |
| UX7 | S10, F9 | handoff al front (`erp-mvp`) | DONE | Los permisos vienen de la API (depende de T6/T8 de `roles-permissions`): desaparece el botón que se ve y el back responde 403 — commit `erp-mvp 3907340` (`permissionStore` lee `/api/permissions`; `usePermission`) + el back ya expone el contrato (`286dd00`, `296777d`) |
| UX8 | S1, F12 | handoff al front (`erp-mvp`) | PENDING | El test de claves i18n cubre `ventas.*` también, y las claves nuevas de UX1-UX5 se agregan en `es` y `en` |
| UX9 | S1, F1 | handoff al front (`erp-mvp`) | PENDING | La ruta `/pos` y el menú con grupos: **la lista de módulos sigue igual** ✓ → lo que cambia es el **dibujo** ✓ → un módulo con pantallas se dibuja como **grupo desplegable** ✓ (chevron ✓, se abre solo cuando contiene la ruta activa ✓, el hijo activo se resalta ✓) → el sidebar no crece con tantos botones ✓ → "Ventas" recibe al **POS** (`/pos` ✓) · Contabilidad ✓, RRHH ✓ y Facturación ✓ (que ya tienen sub-rutas hoy ✓) entran como grupos ✓ → `src/pages/POS/` se llena con la pantalla del POS |
| UX10 | S1, F6 | handoff al front (`erp-mvp`) | PENDING | El total que muestra la caja es el que el back recalcula (con ITBIS): la pantalla deja de mostrar un número y enviar otro |

| UX11 | S1 | handoff al front (`erp-mvp`) | PENDING | El módulo `side-menu` entra al **template del front** (`erp-mvp/FRONTEND-SKELETON-TEMPLATE.md` ✓) con el porqué ✓ → es la manera de tener un **menú reutilizable** ✓ (PRINCIPIO: el menú se construye desde las rutas ✓ · el filtrado de permisos en un solo lugar ✓ · sin ids internos en la app ✓) → también la carpeta en el §1.1 ✓, el paso del §3.1 ✓ y las convenciones del §4 ✓ → **va junto con la integración, no al final** ✓ (los templates son la fuente única ✓ y van par a par con el desarrollo ✓) |

| UX12 | S13 | inline (parent) + handoff al front (`erp-mvp`) | **BACK CERRADO** (2026-11-24) · front pendiente | El **RNC del comprador es de la venta**: `CreateSaleRequest`/`UpdateSaleRequest` llevan `ReceptorTaxId` → la venta lo guarda y `ResolveReceptorTaxIdAsync` solo lo resuelve desde la ficha del cliente cuando la venta **no** trae uno. La caja pide el RNC cuando el cliente no existe y **lo deja en la venta**; el padrón de clientes solo se toca cuando el cliente **quiere quedar registrado** (un casillero). Las validaciones: **E31 y B01 exigen RNC** (siempre) y **el E32 de RD$250,000 para arriba también** (la caja avisa antes de firmar); el **E32 por debajo** y el **B02** van sin RNC (consumidor final). El **back primero** (contrato) → el front después |

### UX12 — subtareas (2026-11-24)

| ID | S# linked | Route | Status | Commit / evidencia |
|---|---|---|---|---|
| UX12.0 | S13 | inline (parent) | DONE | `e76d798` · el trabajo que quedó sin commitear de la sesión anterior (la lista de ventas pendientes trae sus líneas) → unidad propia, antes que UX12 |
| UX12.1 | S13 | inline (parent) | DONE | RED observado: 6 tests nuevos en rojo antes de implementar (la regla del dominio, la resolución del RNC en create/update y la regla del consumo desde RD$250,000) |
| UX12.2 | S13 | inline (parent) | DONE | `3183ea9` · `CreateSaleRequest`/`UpdateSaleRequest` llevan `ReceptorTaxId` · `ResolveReceptorTaxIdAsync` solo rellena desde el cliente cuando la venta **no** trae uno · el update **no borra** el RNC que la venta ya tenía |
| UX12.3 | S13 | inline (parent) | DONE | `3183ea9` · `BuyerTaxIdPolicy` + `Sale.Complete()`: E31 y B01 exigen RNC siempre · E32 y B02 lo exigen desde RD$250,000 · falla cerrado **antes** de firmar el e-CF o de quemar el número del NCF · el mensaje del E31 no cambió |
| UX12.4 | S13 | inline (parent) | DONE | `3183ea9` · suite completa **1141 verdes · 0 rojos · 9 saltados** (los que necesitan Postgres real) + build sin warnings |
| UX12.5 | S13 | handoff al front (`erp-mvp`) | PENDING | **Fase 2, después del back** (`D:/source/my_projects/erp-mvp` ✓): la caja pide el RNC y lo deja en la venta · el padrón de clientes solo se toca si el cliente quiere quedar registrado |

## Riesgos

- **item 3 (contrato)**: UX1/UX2 cambian el payload y el flujo del front (`POST /api/sales` + `PUT /api/sales/{id}/complete`), y hay **otro agente trabajando el front ahora**: la entrega tiene que ser una rama aparte en `erp-mvp` y coordinarse.
- **item 1 (datos)**: UX0 toca el descuento de stock: hay 9 ventas pendientes y 36 completadas con el doble descuento (arrastre del plan de e-CF). El bugfix no arregla las filas: la migración de datos es decisión aparte del owner.
- **item 2 (seguridad)**: la venta completada sin documento fiscal es un hueco fiscal: hay que decide qué pasa con las ventas que el POS creó como `Completed` sin pasar por el complete (hoy no hay ninguna, porque el default es `pendiente` — a verificar contra los datos reales).
- **NO item 6**: la capa de e-CF tiene 390+ tests y el front tiene tests de i18n y de stores: las tareas del front son verificables.

## Log

### L1 — 2026-11-20

> "Champ vamos a continuar con la implementacion de la matrix de permisos que dejamos aye, tambien vamos a ver el front si necesita algo, mira ayer hologamos el front pero me gustaria que valides algo tenemos la pantalla de invoicing donde ahi basicamente hacemos todo lo de la facturacion electronica pero me gustaria que sea mas user friendly intuitivo para el usuario, validemos como la hace alegra y esos sistemas que ya tienen una posicion en el mercado, porque en ventas tenemos la parte de nueva venta desde ahi el POS es que vendemos por eso creo que ahi es que debemos prender o apagar la facturacion electronica, pero debemos hacer un plan para hacer esas mejoras."

### L2 — 2026-11-20 — validación de mercado y lectura del código (parent)

La validación (S2) y el mapa del front se hicieron con dos exploradores de solo lectura, uno sobre la documentación pública de los sistemas y otro sobre el código de `erp-mvp` (nada se modificó en ese repo). Fuentes primarias: ayuda de Alegra (POS, numeración, estados, factura sin internet), PDFs de la DGII (Paso a Paso del Facturador Gratuito, Guía 6, Representación Impresa, FAQ técnicas), docs de Adm Cloud, páginas públicas de Digisoft y Kuadre, documentación de Odoo (MX/PE) y de QuickBooks. Lo que no está documentado por el proveedor está marcado como "no documentado" en la tabla de arriba; nada se inventó.

**El hallazgo que reencuadra S3**: la intuición del owner es correcta en que **el lugar de la decisión es la caja**, e incorrecta en que **lo que se prende y apaga sea por venta**. El interruptor legal es el modo de la empresa (`Electronic`/`Normal`), que es una declaración ante la DGII; el back rechaza la factura comercial para un contribuyente en modo electrónico (`SaleService.cs:267`). Lo que sí se elige por venta es **el documento** (E32/E31/comercial), que es exactamente el modelo de numeración de Alegra.

**El hallazgo que reencuadra S1**: la pantalla que más necesita ser "user friendly" **no es la de facturación**: es la de nueva venta, porque hoy el POS **no completa ninguna venta** (F4, F5) y por lo tanto **no emite ni un comprobante**. La pantalla de facturación es administración (emisores, rangos, certificados) y está bien como está; lo que le falta es el estado de *readiness* (S9).

**El bloqueo que une las dos features**: F10. El doble descuento de stock está vivo en el back, pero el POS no completa ventas → el bug está latente. Conectar el complete (UX1) lo vuelve visible en todas las ventas del POS. UX0 va primero, y es el mismo bugfix que el plan de `roles-permissions` tenía anotado como bloqueo del POS (apéndice de `odd/tasks/roles-permissions.md`).

**Decisión de encuadre**: esta feature **no se implementa todavía**. El owner pidió un plan; el plan está arriba, con el bloqueo previo y la dependencia de `roles-permissions` (T6/T8) señaladas.

### L3 — 2026-11-20 — UX0: el stock no se descuenta dos veces

> (el dueño: *"el POS puede dejar una venta pendiente y luego la completa el mismo vendedor si ya el usuario no quiere nada mas"*)

- **El modelo del inventario de una venta** ✓: **crear = reservar** (el stock se descuenta) → **completar = convertir la reserva** (ya no se descuenta otra vez) → **liberar** (al cancelar o al borrar, que ya existe ✓).
- **El bug** ✓: la creación descontaba y la **completación descontaba otra vez** → cada venta del POS iba a descontar el doble. Estaba **latente** porque el front nunca completaba una venta ✓ → en cuanto se conecte el `complete` (UX1) aparece en todas ✓ → por eso UX0 va primero ✓.
- **El arreglo** ✓: la completion ya no toca el stock (`SaleService.CompleteSaleAsync` ✓) → el stock que se descontó al crear **es** la venta ✓.
- **Un test que fijaba el bug** ✓: la completion fallaba por falta de stock (porque descontaba de nuevo) → ese test ahora dice lo que tiene que decir ✓ (la reserva se hizo al crear ✓) y hay otro que dice que la completion **no** vuelve a descontar ✓.
- **Tests** ✓: suite completa **1103/1103** ✓.

**Lo que falta del ciclo**: **liberar** la reserva de una venta pendiente que el cliente nunca se lleva (hoy no hay endpoint de cancelar ✓ → el POS necesita el botón de "descartar" ✓, que va con UX1) y la **expiración** de las ventas pendientes que quedan abiertas (eso pertenece al cierre del turno ✓ → T10 / la feature del POS).

**Risk**: item 1 (datos) — el cambio afecta el stock de las ventas **que se completen** a partir de ahora ✓; las ventas ya completadas con el doble descuento **no se corrigen solas** (9 pendientes y 36 completadas con el doble ✓ → la migración de datos es una decisión aparte ✓).

### L4 — 2026-11-20 — UX2, UX3 y UX4: el documento en la caja y el estado fiscal con las palabras de la DGII

> (el dueño: *"el default te lo dicen los documentos, consumidor final creo"*)

- **El default del comprobante es el de CONSUMO (E32)** ✓ → y no es una opinion: los documentos de la DGII (`docs/Representacion-Impresa-Modelos.txt`, §2) dicen que la **Factura de Consumo Electrónica es una "factura sin valor fiscal"** y dan **dos modelos ilustrativos**: una con total **igual o mayor a DOP 250 mil** (2.1) y otra **menor a DOP$ 250 mil** (2.2) → **es válida a cualquier monto** ✓ → el default es el de Consumidor Final ✓ y el que decide por el monto es el backend (RFCE bajo los 250 mil ✓, `RfceDocument.MaximumAmount` ✓).
- **El crédito fiscal (E31) es para el cliente que lo necesita** ✓ y **exige su RNC** ✓ → si el vendedor lo elige y el cliente no tiene RNC, la caja lo avisa y no completa ✓ (el backend también lo rechaza ✓).
- **La factura comercial** (sin comprobante) es para quien factura en modo normal ✓ (el backend la rechaza en modo electrónico ✓).
- **UX3** ✓: la venta tiene **columna propia** para el estado fiscal (con el vocabulario de la DGII y su color ✓), **aparte del estado de pago** ✓; el detalle trae el **veredicto**, el e-NCF, el track id y el **QR** (descargado con el token y revocado al cerrar ✓).
- **UX4** ✓: el **reintento** aparece solo con el comprobante rechazado o que requiere revisión ✓ y llama al `retry` de la API ✓ (el desvío de 5 clics a Contabilidad desaparece ✓).
- **Un hueco del back que encontró el trabajo** ✓: el estado grueso de la venta (`fiscalState`) es **nulo para los veredictos cerrados** (a propósito: es "lo que está abierto" ✓) → la lista no mostraría "Aceptado" ✓ → **la venta responde ahora también `fiscalStatus`** (el estado del comprobante ✓) y el estado grueso **no cambia** ✓ (el filtro de los comprobantes abiertos usa la lista de estados cerrados ✓) → commit `2066c22` ✓.
- **Tests** ✓: front **434/434** ✓ · back **1104/1104** ✓.

**Lo que queda de esta tanda**: los estilos del chip y de la tarjeta fiscal quedaron **en línea** (los CSS de los screens no estaban entre las superficies permitidas ✓) → se pueden mover a los módulos ✓.

**Riesgo**: item 3 (contrato) — la venta responde un campo nuevo (`fiscalStatus` ✓) y el front lo consume ✓ → **el back primero** ✓. Y el merge de la rama de e-CF del front va a pedir resolución manual en `VentasList.tsx` y `VentaDetail.tsx` (las dos ramas tocaron el estado fiscal de la venta ✓).

### L5 — 2026-11-21 — reanudación: el back expone el "listo para facturar"

> (el dueño: *"continuemos donde quedamos"*)

- **Dónde quedamos** ✓: el módulo e-CF del front ya está en `development` (`erp-mvp 570e5f7`, `e62f616`) ✓ → la parte de front de **UX1** (el botón de cobrar completa la venta ✓) y de **UX7** (los permisos los da la API ✓) está hecha ✓ → el tracker queda al día ✓.
- **Lo que arranca hoy** ✓: **UX6** (back, inline) → `FiscalReadinessDto` con el modo de emisión, el certificado (existe/vence), los rangos con cupo, la contingencia y `IsReady` + la lista de bloqueos ✓ → `GET /api/ElectronicInvoices/readiness` ✓ (permiso `ElectronicInvoicingRead` ✓ ya está en el mapa de rutas ✓).
- **Lo que sigue** ✓: UX5 (checklist en Configuración y en la caja, alimentado por UX6 ✓) → UX8 (i18n) ✓ → UX10 (los totales de la caja ✓) → UX9 (la decisión sobre `src/pages/POS/` vacío y las ventas pendientes ✓ → la del dueño ✓) → T10 (formalizar el POS como feature aparte ✓).
- **Riesgo** ✓: item 3 (contrato) — la venta ya responde `fiscalStatus` ✓ y ahora la API responde un endpoint nuevo ✓ → **el back primero** ✓, el front lo consume después ✓.

### L6 — 2026-11-21 — el dueño encuadra el producto: la FE es un módulo opt-in y el camino normal es de primera clase

> (el dueño: *"porque no todos los negocios colmadones, mini markets y micro o medianos que quieran el sistema van a facturar electronicamente"* · *"por eso el sistema es modular si quieres la facturacion electronica se te habilita sino sigues facturando normal"* · *"osea el sistema ya de por si esta pensado para la FE, pero tambien si el cliente no desea FE puede facturar normal con el mismo sistema, es a eso que me refiero por eso nos NCF deben tambien usarse normal los b02, b01 etc"*)

- **El encuadre** ✓: el sistema **nace pensado para la FE** ✓ → el default de `Company.EmissionMode` **no se toca** ✓ (una empresa nueva nace en `Electronic` ✓) → el que no la quiere la cambia a `Normal` y factura con NCF ✓.
- **Lo que cambia en UX6** ✓: el readiness deja de ser "el módulo electrónico" y pasa a ser **"el camino que la venta toma hoy"** ✓ → en modo normal o en contingencia el checklist es el del **NCF (B01/B02)** ✓ → **el modo deja de ser un blocker** ✓ (facturar normal es un derecho ✓) → la respuesta dice `usesElectronicEmission` ✓ → y el tipo de documento se nombra como el contribuyente lo nombra (**E32**, **B02**) ✓.
- **B01 y B02 no son formularios: son los tipos de NCF** ✓ (Guía del Contribuyente No. 5 ✓: *"Factura de Crédito Fiscal (Tipo B01, E31)"* y *"Factura de Consumo (Tipo B02, E32)"* ✓) → emitir el NCF ya existe en el código ✓ (`NormalFiscalDocumentService` ✓ + `NormalFiscalSequence` ✓) → **lo que falta es reportarlo** ✓ → feature nueva: `odd/tasks/reportes-dgii.md` ✓ (la 607 con el detalle de B01/B02, el resumen de consumo, la 606 de compras, la 608 de anulados y el RFCE ✓).
- **Riesgo** ✓: item 1 (datos) — los reportes se remiten con los números que el contribuyente ya usó ✓ → el período cerrado se congela ✓ (task RD1 ✓).

### L7 — 2026-11-21 — UX6: el back dice si el contribuyente puede facturar hoy

- **El endpoint** ✓: `GET /api/ElectronicInvoices/readiness` ✓ → `FiscalReadinessDto` ✓ (modo ✓ · si emite electrónicamente hoy ✓ · contingencia ✓ · certificado ✓ · `isReady` ✓ · bloqueos ✓ · los tipos con su cupo ✓) → commit `a0e6507` ✓.
- **El camino, no el módulo** ✓: el readiness sigue `CompanyEmissionPolicy.UsesElectronicEmission` ✓ → modo electrónico → **certificado + rango de e-NCF** (E31/E32/E33/E34) ✓ → modo normal o contingencia → **rango de NCF** (B01/B02/B03/B04) y **nada de certificado** ✓ → **el modo no es un bloqueo** ✓.
- **Los tipos con el nombre del contribuyente** ✓: el código es **E32** o **B02** ✓ (no el número del enum ✓) → el front no tiene que adivinar de qué enum viene ✓.
- **Los bloqueos, con su motivo** ✓: sin certificado ✓ · sin rango ✓ · rango expirado ✓ · rango agotado ✓ · rango anulado ✓ · sin fecha de expiración cuando el tipo la exige ✓ → los mismos motivos que dice la asignación de números ✓ (`ElectronicInvoiceSequenceRepository.NoRangeAsync` ✓).
- **Tests** ✓: 10 de servicio ✓ + 3 de endpoint ✓ (la empresa sale del token ✓ y la respuesta de un tenant nunca menciona a otro ✓) → suite **1108/1108** ✓.
- **Riesgo** ✓: item 3 (contrato) — la API responde un endpoint nuevo ✓ → **el back primero** ✓ → el front lo consume en UX5 ✓.

### L8 — 2026-11-21 — las decisiones del dueño: los reportes que entran y el lugar del POS

> (el dueño: *"la recomendacion en el 1, en la 2 basicamente el POS vive dentro de ventas y se llama a traves del nueva venta, pero me gustaria tener la ruta POS pero como si fuera un menu items para ventas, Ejemplo: Ventas: POS"* y *"eso es solo una referencia no es que tal cual va asi"*)

- **RD0 — los reportes** ✓: se acepta la recomendación ✓ → **la 607 de ventas entra primero** ✓ (el detalle de los B01 ✓ + el de los B02 ≥ RD$250,000 ✓ + el resumen de las de consumo menores ✓) → después la **608** ✓ → después la **606** ✓ (la grande ✓) → el **RFCE** con la T29 del plan de e-CF ✓ → feature `reportes-dgii` ✓.
- **UX9 — el lugar del POS** ✓: el POS **vive dentro de Ventas** ✓ (se abre con "Nueva venta" ✓) **pero tiene su propia ruta** (`/pos` ✓) **y aparece como ítem de menú del grupo "Ventas"** ✓ → el sidebar deja de ser una lista plana y pasa a **grupos con submenú** ✓ → la estructura del HTML que pasó el dueño ✓ → **es referencia de estructura, no de estilo** ✓ (los colores y las formas son los de la app ✓).
- **El menú, en palabras del dueño** ✓: *"la lista sigue solo que ahora va hacer como si fuera un submenu asi el side bar no crece con tantos botones"* ✓ → la lista de módulos **no cambia** ✓ → lo que cambia es el **dibujo** ✓ → un módulo con pantallas se dibuja como **grupo desplegable** ✓ → el sidebar deja de crecer con botones ✓.
- **Lo que sigue esperando** ✓: las **ventas pendientes** (la otra mitad de UX9 ✓) → sin respuesta todavía ✓.

### L9 — 2026-11-22 — el dueño ve la venta duplicada y define el ciclo del borrador

> (el dueño: *"el borrador es una venta que no se ha completado pero al completarla es la misma venta la que cambia de pendiente a completa"* y *"cuando se completa se queda el cliente y el item en el carrito debe borrarse como cuando se salva como borrador"*)

- **La regla del ciclo** ✓: el borrador **es** una venta pendiente ✓ → al completarla, **esa misma venta** pasa de pendiente a completa ✓ (no se crea otra ✓) → el front recuerda su venta y la actualiza ✓.
- **La caja se limpia en los dos caminos** ✓: guardar borrador **y** completar limpian renglones y cliente ✓.
- **El bug de fondo (back)** ✓: el repositorio base lee sin tracking ✗ → `s.Items = newItems` + `Update(entity)` ✗ → las filas viejas nunca se borran ✗ → cada actualización **sumaba** un juego de líneas ✗ → productos repetidos ✓, unidades infladas ✓ y **stock devuelto una vez por línea vieja** ✓. El arreglo: `SaleRepository.UpdateSaleAsync` carga las líneas persistidas con tracking y **borra las que ya no están** en la venta ✓.
- **La venta sin comprobante** ✓: el backend no acepta `documentType: null` ✗ → la venta guarda el tipo **+ la bandera `IsCommercialInvoice`** ✓ → y la factura comercial en modo electrónico **ya no se rechaza** ✓ (la venta se crea como el cajero la elige ✓).
- **Pendiente al cierre de la sesión** ✓: el back quedó en el working tree **sin commitear** ✗ (8 modificados + 4 nuevos ✓) → falta la suite completa en verde ✓ → el commit ✓ → y el push de los dos repos ✗ (el guard RDD exige el binario nativo que el update del shell dejó fuera ✓).

### L10 — 2026-11-23 — el RNC del comprador: qué valida el back y qué falta

> (el dueño: *"si yo quiero facturar con comprobante fiscal uso un NCF si el clente no exite le pido su RNC y le hago la venta"* · *"osea nuestro sistema debe hacer eso, pero valida todo"* · *"Exacto asi seria lo correcto a menos que el cliente quiera que lo registren"* · *"pero como es un sistema ERP la mayoria de mis clientes van a estar registrados ya eso seria un caso aislado"*)

**La regla de la DGII, validada en la red** ✓ (no de memoria ✓): el **NCF es el número**, no el documento ✓ → 11 caracteres en papel (`B` + 2 del tipo + 8 de secuencia ✓) y 13 en el electrónico (`E` + 2 del tipo + 10 ✓) → el **E31 y el B01 exigen el RNC del comprador siempre** ✓ → el **E32 lo exige desde RD$250,000** (por debajo el comprador es **opcional**: consumidor final ✓) → el **B02 no lo exige** ✓.

**Lo que el back ya hace** ✓: el E31 sin RNC se rechaza (`SaleEcfDocumentMapper.MapBuyer:166` → `profile.RequiresBuyerTaxId` ✓ → el perfil sale del **XSD oficial** (`ecf-31.xsd`: `RNCComprador` con `minOccurs="1"` ✓)) → el E32 sin RNC se emite sin la sección Comprador ✓ (`ecf-32.xsd`: `minOccurs="0"` ✓) → el sistema **ya** decide por los RD$250,000 (`ElectronicInvoiceService.VerificationUrlOf:1095` ✓: el timbre del resumen del RFCE abajo, el del host del contribuyente arriba ✓) → el NCF de papel no exige receptor ✓ (`NormalFiscalDocumentService:79` ✓) → la caja ya avisa cuando el cliente no tiene RNC ✓ (`erp-mvp/.../NuevaVenta.tsx:153-155, 260` ✓).

**Lo que falta** ✗: el **E32 de RD$250,000 para arriba sin RNC no se rechaza** ✗ (la regla del monto no está en el XSD: es de la DGII ✓ → la DGII lo rechaza al enviarlo ✓) → el **B01 sin RNC no se valida** ✗ (el camino de papel no usa el perfil del XSD ✓) → la venta **no tiene campo** para el RNC del mostrador ✗ (`CreateSaleRequest`/`UpdateSaleRequest` no lo llevan ✓ → y `ResolveReceptorTaxIdAsync` sobrescribe con `client?.TaxId` ✓) → el **RFCE** (T29c del plan de e-CF ✓) no se envía al host de consumo ✗ → el E32 bajo RD$250,000 sigue por el camino del e-CF y **su timbre no resuelve en la DGII** ✗ (el trabajo está guardado en `wip/rfce-y-bugs-2026-11-21`, `595567a`, sin integrar ✓).

**Lo que se decidió** ✓: el RNC que el cajero pide a un cliente que **no** está registrado **queda en la venta** ✓ → el padrón de clientes solo se toca cuando el cliente **quiere quedar registrado** ✓ → y como en un ERP el cliente **ya está registrado**, ese caso es el **aislado** ✓ → lo que pega en todas las ventas es la **validación** (UX12 ✓).

**Riesgo** ✓: item 3 (contrato) — la venta acepta y expone un RNC propio ✓ → **el back primero** ✓ → el front después ✓ · item 1 (datos) — el RNC de una venta queda en esa venta ✓ (no toca el padrón de clientes salvo que el cajero lo marque ✓).

### L11 — 2026-11-24 — UX12 arranca: el back primero, el front después, sin paralelo

> (el dueño: *"Ok comencemos pero no hagas trabajo paralelo si es el backend primero y luego el front o viceversa"*)

- **La instrucción** ✓: **nada de trabajo en paralelo** ✓ → el **back primero** ✓ (es el contrato) → el **front después** ✓ (la caja), cada uno en su repo y en su rama ✓.
- **Alcance del back (UX12)** ✓: (1) `CreateSaleRequest`/`UpdateSaleRequest` llevan `ReceptorTaxId` ✓ → la venta lo guarda ✓ → `ResolveReceptorTaxIdAsync` solo lo resuelve desde la ficha del cliente **cuando la venta no trae uno** ✓ → (2) las reglas: **E31 y B01 exigen RNC siempre** ✓ · **E32 y B02 lo exigen desde RD$250,000** ✓ (el E32 y el B02 de RD$250,000 para arriba son nominativos ✓) · por debajo van sin RNC (consumidor final ✓).
- **Dónde se validan** ✓: en el dominio (`Sale.Complete()` ✓) → el mismo camino para el e-CF y para el papel ✓ → **falla cerrado antes de quemar el número** ✓ (el e-CF se firma y el NCF se numera después ✓) → el E31 ya se validaba ahí ✓ → la regla nueva se suma ✓ · el umbral es el mismo que el del RFCE (`RfceDocument.MaximumAmount` ✓) → una sola constante ✓.
- **Lo que NO cambia** ✓: la respuesta de la venta ya expone `receptorTaxId` ✓ (`SaleDto` ✓) → el front ya lo lee ✓ → **no hay migración** (la columna existe ✓).
- **Decisiones del padre, escritas para que no se pierdan**: la **validación de formato del RNC** no se agrega en esta unidad ✓ (el RNC del mostrador se guarda recortado ✓ · el formato ya se valida en el padrón de clientes ✓) → y la regla **no se aplica al guardar el borrador** ✓ (la caja puede guardar el borrador y pedir el RNC al completar ✓ · lo que se rechaza es la **emisión** ✓) → el front avisa antes (fase 2 ✓).
- **Riesgo** ✓: item 3 (contrato) — la API acepta un campo nuevo en `POST /api/sales` y en `PUT /api/sales/{id}` ✓ → **el back primero** ✓ → el front después ✓ · item 1 (datos) — el RNC queda en esa venta ✓ (no toca el padrón de clientes ✓).
