# Los reportes que la DGII le pide al contribuyente: 607, 606, 608 y el resumen de consumo

Feature: `reportes-dgii`
Rama: `development`
Delivery: ask-on-risk
Test runner: `dotnet test Volterp.Tests/Volterp.Tests.csproj`

Objetivo: que el sistema produzca lo que la DGII pide periódicamente (la 607 con el detalle de las ventas, el resumen de facturas de consumo, la 606 de compras, la 608 de comprobantes anulados) sin que el contador exporte a mano.

Estado: **PLAN. Nada implementado.** El dueño lo señaló como un hueco ("hay que tener en cuenta las B01 y B02 etc."); la investigación de abajo es lo que se sabe hoy.

## Specs

### S1

> "hay que tener en cuenta las B01 y B02 etc."

### S2

> "por eso el sistema es modular si quieres la facturacion electronica se te habilita sino sigues facturando normal"

### S3

> "osea el sistema ya de por si esta pensado para la FE, pero tambien si el cliente no desea FE puede facturar normal con el mismo sistema, es a eso que me refiero por eso nos NCF deben tambien usarse normal los b02, b01 etc"

### S4

*Derivada de S1 y verificada en la investigación*: **B01 y B02 no son formularios de la DGII: son los tipos de NCF**. B01 = Factura de Crédito Fiscal (E31 en electrónico) y B02 = Factura de Consumo (E32). El código ya lo dice: `Volterp.Domain/Enums/NormalFiscalDocumentType.cs:8-13` ("the credit fiscal invoice is 31 electronically and B01 on paper") y el `NormalFiscalSequence` es "el rango de NCF" (`NormalFiscalSequence.cs:5-8`).

Consecuencia: lo que al dueño le preocupa **no es emitir el NCF** (eso ya existe: `NormalFiscalDocumentService.IssueAsync` + `NormalFiscalSequenceRepository`), **es reportar esos NCF a la DGII cada mes**.

### S5

*Derivada de S1, verificada contra los documentos oficiales*: los formularios que un contribuyente pequeño y mediano tiene que remitir:

| Formulario | Qué es | Vence | Canal |
|---|---|---|---|
| **606** | Compras de bienes y servicios (costos, ITBIS al costo, retenciones) | mensual, día 15 | Oficina Virtual (TXT) |
| **607** | Ventas de bienes y servicios: **el detalle de los B01** y de los **B02 ≥ RD$250,000** (con la identificación del comprador) | mensual, día 15 | Oficina Virtual (TXT) |
| **607 / módulo "Resumen General de Facturas de Consumo"** | Los **B02 < RD$250,000** van agregados: cantidad de NCF, monto facturado, ITBIS facturado, ISC, otros impuestos, propina | mensual, día 15 | módulo de la OFV |
| **608** | Comprobantes anulados (NCF anulados y la razón) | mensual, día 15 | Oficina Virtual (TXT) |
| **609** | Pagos al exterior | mensual, día 15 | Oficina Virtual (TXT) |
| **IT-1 + Anexo A** | ITBIS | mensual, día 20 | OFV (primero los formatos de envío, después el Anexo A, después el IT-1) |
| **IR-17** | Retenciones de ISR a terceros | mensual, día 10 | OFV |
| **IR-3 / IR-13** | ISR retenido a asalariados / resumen anual | mensual día 10 / anual 15-mar | OFV |
| **IR-1 / IR-2** | ISR anual de persona física / jurídica | 90 / 120 días tras el cierre | OFV |

El detalle de la 607 son 23 columnas; la 606 tiene su instructivo propio. Fuentes: *Guía informativa sobre los Formularios de Envío de Datos* (mayo 2026), *Paso a paso envío de datos* (marzo 2026), *Instructivo de llenado y envío del formato 607*, *Guía del Contribuyente No. 5 – Comprobantes Fiscales* (mayo 2022).

### S6

*Derivada de S5, verificada contra la documentación oficial*: **el e-CF reemplaza a la 607 de ventas, pero no a la 606 de compras**.

- Guía para el Cumplimiento Tributario de Nuevos Contribuyentes (dic 2025): *"El Formato 607 se usa para enviar las ventas de bienes y servicios. Si emites facturas electrónicas no tienes la obligación de remitir este formato."*
- El informe técnico del e-CF lo dice del lado de las compras: los documentos recibidos se reportan en la 606 (`docs/Informe-Tecnico-eCF-v1.0.txt:534-536`).
- El resumen de las facturas de consumo electrónicas **menores de RD$250,000** va por otro camino: el **RFCE** (Recepción de Resumen Factura de Consumo e-CF), que es un XML por factura, no un agregado (`docs/DescTec-Servicios-DGII.txt:423-425, 520-533`).
- Si el contribuyente no tuvo operaciones, los formatos se remiten **en cero** (606/607/608).

Lo que **no** está documentado (y hay que confirmar con la DGII o con el contador): si el 608 sigue aplicando a los emisores electrónicos, y si el emisor electrónico sigue usando el módulo de "Resumen General de Facturas de Consumo".

## Hallazgos con evidencia (lo que el sistema ya tiene y lo que no)

### Lo que ya está

| Dato | Dónde | Para qué sirve |
|---|---|---|
| Fecha del comprobante | `Sale.BusinessDate` (`Sale.cs:20`) | columna "Fecha Comprobante" de la 607 y el período |
| Identificación del comprador | `Sale.ReceptorTaxId` (`Sale.cs:28`), `Client.TaxId` (`Client.cs:15`), tipo en `Rnc.Type` (`Rnc.cs:36,60-70`) | columnas 1-3 de la 607 (**no hay tipo "Pasaporte"**) |
| Número del comprobante | `ElectronicInvoice.ENCF` (`ElectronicInvoice.cs:13`), `NormalFiscalDocument.Number` (`NormalFiscalDocument.cs:15`) | columna 3 de la 607 (e-NCF para el electrónico, NCF para el de papel) |
| Comprobante modificado | `Sale.ModifiedENCF` (`Sale.cs:36`) | columna 4 de la 607 |
| Monto y ITBIS | `Sale.Subtotal` / `Sale.Itbis` (`Sale.cs:46-47`) | columnas 8-9 de la 607 |
| ITBIS por tasa | `SaleItem.TaxCode` / `Subtotal` (`Sale.cs:149-150`) + `EcfTaxRates` (18/16/0) | bases y ITBIS por tasa (RFCE y Anexo A) |
| Código de seguridad | `ElectronicInvoice.SecurityCode` (`ElectronicInvoice.cs:27`) | `CodigoSeguridadeCF` del RFCE |
| Tipo de comprobante | `Sale.DocumentType` | B01 o B02 |
| Anulación | `ElectronicInvoice.Annull()` y `Status` | los candidatos de la 608 |

### Lo que no está (y por eso los reportes no se pueden producir)

| Falta | Detalle | Formulario que lo bloquea |
|---|---|---|
| **Ninguna entidad de período fiscal** | No hay un período que cierre el mes: solo `Volterp.Domain/Time/ReportingPeriod.cs:16`, que es un helper del dashboard | 606, 607, 608 |
| **Ninguna fila de "reporte de ventas al detalle"** | Nada produce las filas de la 607 | 607 |
| **Sin NCF ni RNC del proveedor en la compra** | `Supplier` no tiene `TaxId` (`Supplier.cs:8-20`) y `Purchase` no tiene NCF | 606 |
| **Sin tasa de ITBIS en los ítems de compra** | `PurchaseItem` no tiene `TaxCode` (`PurchaseItem.cs:8-16`) | 606 |
| **Sin forma de pago ni fecha de pago en la venta** | La 607 pide las formas de pago (efectivo, cheque, tarjeta, crédito, bonos, permuta) en 7 columnas; `Sale` no las tiene (`RfceIdentity.Payments` existe en el modelo del RFCE pero no tiene origen en el dominio) | 607, Anexo A |
| **Sin "Tipo de Ingreso"** | La 607 pide una de 6 categorías de ingreso | 607 |
| **Sin razón de anulación** | `NormalFiscalDocument` no tiene estado ni bandera de anulado; `Sale.Cancel()` solo funciona desde `Pending` (`Sale.cs:74-79`) | 608 |
| **El RFCE no está conectado** | `RfceXmlWriter` solo se usa dentro de `Volterp.ElectronicInvoicing` y en el QR (`ElectronicInvoiceService.cs:940`): no hay mapeo de la venta al documento RFCE ni llamada al web service | RFCE (T29 del plan de e-CF) |

## Propuesta: el período fiscal (RD1) — a la espera del OK

El período fiscal es la base de las dos cosas: el archivo que se sube a la OFV sale de un período, y el RFCE se numera dentro de un período.

**La entidad** (`FiscalPeriod`, tenant-scoped):

| Campo | Para qué |
|---|---|
| `Id`, `CompanyId` | el período es de una empresa |
| `Year`, `Month` | el mes (el período fiscaltitel es el mes) |
| `Status` | `Open` / `Closed` |
| `RfceSequence` | el consecutivo del **RFCE** del período: cada factura de consumo menor de RD$250,000 reporta una por una (la norma lo dice: “a través del Formulario de Resumen Factura de Consumo Electrónica”), y cada una lleva su número dentro del período |
| `ClosedAt`, `ClosedBy` | quién lo cerró y cuándo |

**Las reglas**:
1. **Un período abierto por empresa.** El que se abre al empezar el mes nuevo.
2. **Cerrado es cerrado**: las ventas de un período cerrado no se tocan (ni se editan ni se anulan) → los números que ya se reportaron a la DGII no pueden cambiar por debajo.
3. **El RFCE se manda por venta** (no agregado): cada factura de consumo menor de RD$250,000 es un XML con su número de secuencia dentro del período.
4. La **anulación** de un comprobante va a la **608 del período en que se anula**, no al de la venta → la 608 cuenta anulaciones, no ventas.

**Lo que hay que decidir** (del dueño):
- ¿El período lo **cierra el usuario** (con un botón que además genera los archivos) o se cierra solo al cambiar de mes? Mi recomendación: **lo cierra el usuario**, porque el cierre es el momento en que se genera lo que se manda.
- ¿Puede un negocio tener **más de un período abierto** (por ejemplo, cuando se atrasa en reportar)? Mi recomendación: **no** — un solo período abierto, y el anterior se cierra al abrir el nuevo.

## L4 — 2026-11-21 — la norma NO habla de "período fiscal": era invención nuestra

> (el dueno: *"lo del periodo fiscal la doc no lo especifica?"*)

Se fue a los documentos de la DGII que ya están en `docs/` a buscar qué dice la norma (no lo que opino el agente ✓). El resultado corrige el plan en tres puntos:

**1. No existe el concepto de "período fiscal"** ✓ → en `docs/` no hay definición, ni plazo, ni cierre, ni consecutividad por período ✓ → **la entidad `FiscalPeriod` que había propuesto era una invención nuestra** ✓ → **no se construye** ✓.

**2. El RFCE es un XML POR FACTURA, no un agregado del período** ✓:
- *"Los contribuyentes que emitan facturas de consumo electrónicas menores a DOP$250 mil ... deberán enviar ... las ventas realizadas mediante este tipo de e-CF, en el formato XML que se establece en este documento"* (`docs/Formato-Resumen-Factura-Consumo-Electronica-v1.0.txt:13-15` ✓).
- El resumen lleva **el e-NCF de esa factura** (`:69-70` ✓) y la consulta a la DGII es por RNC + e-NCF + código de seguridad (`docs/DescTec-Servicios-DGII.txt:675-678` ✓) → **no hay contador por período** ✓ → el campo `RfceSequence` que había propuesto **no tiene respaldo** ✓ → no va.
- El `MontoPeriodo` es un **monto** derivado de la propia factura (`= Monto Total + Monto No Facturable` ✓), no un rango de fechas ✓.

**3. Las 72 horas y los quince días son de la CONTINGENCIA**, no de un período ✓ (`docs/Informe-Tecnico-eCF-v1.0.txt:1524-1525` ✓ y `:1556-1557` ✓).

**Y lo que la norma sí resuelve (lo que yo quería inventar con el período congelado)** ✓: *"Las correcciones de un e-CF serán realizadas únicamente mediante el uso de notas de crédito o débito electrónicas"* (`docs/Informe-Tecnico-eCF-v1.0.txt:575-576` ✓) y la anulación por el web service **solo si el documento todavía no se envió** (`:583-586` ✓) → la trazabilidad la da la norma, no un estado nuestro ✓.

**Lo que queda de RD1**: nada como entidad de período ✓ → el trabajo real es **el RFCE por factura** (que ya es la T29 del plan de e-CF ✓) y, más adelante, **los 606/607/608 generados para un mes** (un filtro sobre las ventas de ese mes, no un período congelado ✓).

**Lo que hay que confirmar con la DGII** (no está en `docs/` ✓): la cadencia y el plazo de los 606/607/608 (la tabla de "mensual, día 15" de la S5 viene de guías externas, no de los documentos del repo ✓) → lo consulta tu esposa, que es de la accounting ✓.

## Propuesta: el RFCE por factura (RD1bis) — a la espera del OK

La norma es clara acá (ver el log L4 ✓): **cada factura de consumo electrónica menor de RD$250,000 manda su propio XML de resumen**, identificado por el e-NCF de esa factura, y se consulta a la DGII por RNC + e-NCF + código de seguridad ✓.

**Lo que hay que construir**:
1. El mapeo de la venta al XML del RFCE (el `RfceXmlWriter` ya existe en la library ✓ → falta el **mapeo desde la venta** ✓ y la **llamada al web service** de recepción ✓, que es la T29 del plan de e-CF ✓).
2. El **código de seguridad** del e-CF viaja en el resumen ✓ (`ElectronicInvoice.SecurityCode` ✓ ya existe ✓).
3. La cola: el resumen se manda **después** de que el e-CF esté firmado ✓ → si el e-CF falla, no hay resumen ✓ → y si el resumen es rechazado, se corrige y se reenvía ✓ (la norma lo dice ✓).
4. El **monto del período** se calcula de la propia factura ✓ (`Monto Total + Monto No Facturable` ✓) → no hay que acumular nada ✓.

**Lo que NO hace falta** (y por eso el trabajo es mucho más chico de lo que parecía): la entidad de período ✓, el contador por período ✓, el congelamiento de ventas ✓ (la norma ya resuelve las correcciones con notas de crédito/débito ✓).

**Lo que hay que confirmar con la DGII** (no está en los documentos del repo ✓): la cadencia y el plazo de los 606/607/608 ✓ → lo consulta tu esposa ✓.

## Tasks

| ID | S# linked | Route | Status | Commit |
|---|---|---|---|---|
| RD0 | S1, S4, S5 | decisión del dueño | DONE | **La 607 de ventas entra primero**: el detalle de los B01 ✓ + el de los B02 ≥ RD$250,000 ✓ + el resumen agregado de las de consumo menores ✓ → después la **608** de anulados ✓ → después la **606** de compras ✓ (la grande: el proveedor no tiene RNC ✓) → el **RFCE** se conecta con T29 del plan de e-CF ✓. Decisión del dueño del 2026-11-21 (log L3) |
| RD1 | S4, S5 | inline (parent) | CANCELADO | El **período fiscal** como entidad (mes que se cierra, estado, consecutivo por período): **la norma no lo define** ✓ → era una invención nuestra ✓ → **no se construye** ✓ (ver el log L4 ✓). Lo que hace falta es el **RFCE por factura** (que ya es la T29 del plan de e-CF ✓) |
| RD2 | S5 | inline (parent) | PENDING | La **fila de la 607**: el lector de las ventas del período con NCF, comprador, fecha, montos e ITBIS, en el orden y con el formato de las 23 columnas del instructivo |
| RD3 | S5, S2 | inline (parent) | PENDING | El **TXT de la 607** y el **agregado del resumen de consumo** (los B02 menores a RD$250,000): el archivo que se sube a la OFV |
| RD4 | S5 | inline (parent) | PENDING | La **606 de compras**: el RNC del proveedor, el NCF de la compra, la tasa de ITBIS del ítem y la fecha/forma de pago. Hoy `Purchase` no tiene nada de eso |
| RD5 | S5 | inline (parent) | PENDING | La **608 de anulados**: la razón y la fecha de la anulación, en el comprobante normal (que hoy no tiene estado) y en el electrónico |
| RD6 | S5 | inline (parent) | PENDING | El **RFCE**: el mapeo de la venta al `RfceDocument` y la llamada al web service de recepción (conecta con T29 del plan de e-CF) |
| RD7 | S2, S3 | handoff al front (`erp-mvp`) | PENDING | La pantalla del período: qué se generó, qué falta y qué se subió. Es el módulo que hoy no existe |

## Riesgos

- **item 1 (datos)**: la 607 y la 606 se remiten con los números que el contribuyente ya usó; un archivo mal formado es un problema con la DGII, no un bug de la pantalla → los archivos se generan contra datos **congelados** del período cerrado.
- **item 3 (contrato)**: el formato del TXT es el que la OFV acepta: un cambio en una columna rompe el envío → el formato se versiona con el instructivo oficial.
- **item 6 (sin test)**: el formato se prueba con las columnas del instructivo, no con un caso real de la OFV → el test fija las columnas y los valores del instructivo, y el primer envío real lo valida la DGII.

## Log

### L1 — 2026-11-21

> "otra cosa champ conversando con mi esposa que es contable ella me dijo que hay que tener en cuenta las B01 y B02 etc."

### L2 — 2026-11-21 — el dueño aclara el modelo del producto

> "porque no todos los negocios colmadones, mini markets y micro o medianos que quieran el sistema van a facturar electronicamente"

> "por eso el sistema es modular si quieres la facturacion electronica se te habilita sino sigues facturando normal"

> "osea el sistema ya de por si esta pensado para la FE, pero tambien si el cliente no desea FE puede facturar normal con el mismo sistema, es a eso que me refiero por eso nos NCF deben tambien usarse normal los b02, b01 etc"

**Lo que se decidió**: el sistema **nace pensado para la FE** (una empresa nueva nace en modo `Electronic` ✓ → el default no se toca ✓) y **el que no quiere FE factura normal con el mismo sistema** ✓ → el camino del NCF (B01, B02) es de primera clase ✓ → **el modo no es un defecto**: el readiness de UX6 dejó de tratar "estoy en modo normal" como un blocker ✓ y pasó a seguir **el camino que la venta toma hoy** ✓ (`CompanyEmissionPolicy.UsesElectronicEmission` ✓ → en contingencia también se factura en papel ✓).

**Lo que se descubrió**: B01 y B02 **no son formularios**, son los tipos de NCF ✓ → lo que al dueño le preocupa es el **reporte** de esos NCF ✓ → esta feature ✓.

**Lo que falta decidir** (RD0): qué formularios entra al sistema primero ✓.

### L3 — 2026-11-21 — RD0: los reportes que entran primero

> (el dueño: *"la recomendacion en el 1"*)

**Decisión**: la **607 de ventas** entra primero ✓ → el detalle de los **B01** ✓ + el detalle de los **B02 ≥ RD$250,000** ✓ + el **resumen agregado** de las facturas de consumo menores ✓ → después la **608** de los comprobantes anulados ✓ → después la **606** de las compras ✓ (que es la grande ✓ porque hoy el proveedor no tiene RNC ✓ ni los ítems de compra tienen tasa de ITBIS ✓) → y el **RFCE** se conecta con la T29 del plan de e-CF ✓.

**Lo que esta feature tiene que dejar listo**: el **período fiscal** (RD1 ✓) es la base de las dos cosas ✓ → sin el, el archivo que se sube a la OFV no tiene de dónde salir ✓.
