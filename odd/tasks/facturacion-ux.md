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
| UX0 | S6, F10 | inline (parent) | PENDING | **Bloqueo previo**: el stock se descuenta dos veces (create + complete). Hoy el POS no completa y por eso no se nota; en cuanto UX1 conecte el complete, toda venta del POS descuenta el doble. Bugfix aislado con sus tests en `SaleServiceTests` |
| UX1 | S6, F4, F5 | handoff al front (`erp-mvp`) | PENDING | El botón "Completar Venta" completa la venta: se llama `PUT /api/sales/{id}/complete` y el `<select>` de estado sale de la caja (o pasa a ser el tipo de documento) |
| UX2 | S5, F2, F3 | handoff al front (`erp-mvp`) | PENDING | La elección del documento en la caja: **Consumo (E32)** / **Crédito fiscal (E31)** / **Comercial** (solo si el modo de la empresa es normal; el back rechaza el resto). Default por tipo de cliente |
| UX3 | S7, S8, F7 | handoff al front (`erp-mvp`) | PENDING | El estado fiscal de la venta con el vocabulario de la DGII, con color, **separado del estado de pago**, con el veredicto y el QR de la venta |
| UX4 | S7, F7 | handoff al front (`erp-mvp`) | PENDING | Un comprobante pendiente o rechazado se resuelve **desde la venta** (reintentar), sin el desvío de 5 clics a Contabilidad |
| UX5 | S9, F8 | handoff al front (`erp-mvp`) | PENDING | "Listo para facturar": checklist de certificado válido, rango con cupo, modo declarado y contingencia activa, en Configuración y en el punto de venta |
| UX6 | S5, S9 | inline (parent) | PENDING | El back expone el estado de Readiness (modo, certificado, rango con cupo, contingencia) para que el front no lo tenga que deducir de tres pantallas |
| UX7 | S10, F9 | handoff al front (`erp-mvp`) | PENDING | Los permisos vienen de la API (depende de T6/T8 de `roles-permissions`): disappears el botón que se ve y el back responde 403 |
| UX8 | S1, F12 | handoff al front (`erp-mvp`) | PENDING | El test de claves i18n cubre `ventas.*` también, y las claves nuevas de UX1-UX5 se agregan en `es` y `en` |
| UX9 | S1, F1 | handoff al front (`erp-mvp`) | PENDING | `src/pages/POS/` está vacío: se llena con la ruta del POS o se borra (hoy es código muerto que promete una pantalla que no existe) |
| UX10 | S1, F6 | handoff al front (`erp-mvp`) | PENDING | El total que muestra la caja es el que el back recalcula (con ITBIS): la pantalla deja de mostrar un número y enviar otro |

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
