# Electronic invoicing (e-CF) — plan

## Objective

Permitir que los clientes de Volterp emitan comprobantes fiscales electrónicos (e-CF) ante la DGII,a través de nuestra API, usando **el certificado digital de cada cliente**.

Branch: `feature/electronic-invoicing` (desde `development` @ `d08a830`).
Delivery strategy: `ask-on-risk` (default). Test runner: `dotnet test`.

## Context — what the regulator actually requires

### Marco legal y plazos

- **Ley 32-23** y **Decreto 587-24** obligan a todos los contribuyentes a emitir e-CF.
- **Grandes y medianos nacionales**: plazo venció el **15 de noviembre de 2025**. Ya están Inside.
- **Pequeños, micro y no clasificados**: el **Aviso DGII 06-26** dao prórroga automática de 6 meses → **la fecha límite vigente es el 15 de noviembre de 2026**. Hoy es 3 de noviembre: **12 días**.

Este segundo grupo es exactamente el "emisor informal" del que habla el usuario.

### Los 3 caminos que ofrece la DGII

| Camino | Qué implica |
|---|---|
| **Sistema de Desarrollo Propio** | El contribuyente se autoriza ante la DGII y aprueba él mismo el proceso de certificación de emisión/recepción de e-CF |
| **Proveedor de Servicios de Facturación Electrónica (PSFE)** | Debe: ser emisor electrónico, tener **≥3 contribuyentes ya certificados como emisores electrónicos**, tener registrada en el RNC la actividad económica de **venta y/o desarrollo de aplicaciones informáticas**, estar al día con obligaciones tributarias, y presentar la solicitud con anexos |
| **Facturador Gratuito** | Herramienta gratuita de la DGII para los contribuyentes que cumplen sus criterios |

### Requisitos de un emisor electrónico

RNC · contribuyente con obligaciones tributarias a su cargo · **certificado digital para procesos tributarios** · exigencias técnicas que disponga la DGII · **Formulario de Solicitud de Autorización vía OFV** · aprobar el proceso de certificación · cumplir la **Norma General 06-2018** sobre Comprobantes Fiscales.

### Estructura del e-CF

- Identificador de **13 caracteres**: `E` + 2 dígitos de tipo + 10 de secuencial. Ejemplo `E310000000001`.
- Tipos (par NCF electrónico):

| e-CF | NCF | Nombre | Emite | Exige RNC del comprador |
|---|---|---|---|---|
| E31 | B01 | Factura de Crédito Fiscal Electrónica | vendedor | **sí** |
| E32 | B02 | Factura de Consumo Electrónica | vendedor | no |
| E33 | B03 | Nota de Débito Electrónica | vendedor | **sí** |
| E34 | B04 | Nota de Crédito Electrónica | vendedor | no |
| E41 | B11 | Comprobante Electrónico de Compras | comprador | no |
| E43 | — | Comprobante Electrónico para Gastos Menores | comprador | no |
| E44 | — | Comprobante Electrónico para Regímenes Especiales | vendedor | no |
| E45 | — | Comprobante Electrónico Gubernamental | vendedor | **sí** |
| E46 | — | Comprobante Electrónico para Exportaciones | vendedor | no |
| E47 | — | Comprobante Electrónico para Pagos al Exterior | comprador | no |

Son **10 tipos**. El **32** es el único que admite el flujo resumido **RFCE**, para montos por debajo de RD$250,000.

## The strategic decision — build vs. integrate

**Decidido (2026-11-03): Option A.** Volterp **no** se certifica como PSFE. Cada cliente se certifica como emisor electrónico con su propio certificado `.p12` a través del PSFE, y Volterp le expone la emisión de sus comprobantes dentro de su propia API. El resto de opciones queda como registro de lo evaluado.

### Option A — Integrar con un PSFE ya certificado (ej. eCF MSeller) — **DECIDIDO**

`eCF MSeller` es un **PSFE certificado por la DGII** (IT SOLUCLICK SRL, RNC 130862346, software MSeller v1.0 tipo EXTERNO).

- API: `https://ecf.api.mseller.app/{TesteCF|CerteCF|eCF}/documentos-ecf`, `POST` con JSON, auth `Bearer {apiKey}`.
- Respuesta: `{ rnc, ecf, internalTrackId, securityCode, qr_url, signedDate }`.
- **El PSFE firma con el `.p12` del cliente, envía a la DGII y guarda el XML 10 años.** Firma XMLDSIG RSA-SHA256, contingencia con reintentos en segundo plano, recepción de e-CF de terceros y aprobación comercial, webhooks de cambios de estado.
- **Multi-tenant pensados para nosotros**: cuenta tipo **Proveedor** que agrupa cuentas de clientes, cada una con su API key y su certificado aislados.
- El `.p12` se sube **una vez** y queda cifrado con **AES-256 en el PSFE**. Nosotros nunca tocamos la llave privada.
- Precio: gratis hasta 250 e-CF/mes por cliente emisor (micro y pequeñas); luego USD 15 / 24 / 41 por mes según volumen. Descuento 10-20% desde 3 cuentas de pago.
- Certificados .p12: `digi.camarasantodomingo.do`.

### Option B — Construir nuestra propia integración (el camino de Nurbansoft)

- Firmamos, enviamos, manejamos recepción, aprobación comercial, contingencia y almacenamiento 10 años.
- **Exige certificarnos como PSFE** ante la DGII: ≥3 contribuyentes ya certificados + actividad de software en el RNC + al día con tributación + solicitud y anexos.
- **Nosotros guardamos el `.p12` y el password de cada cliente** → la llave de su identidad fiscal fiscal vive en nuestro Postgres.
- Control total, sin costo por comprobante. Plazo: meses.

### Option C — Bootstrap híbrido (recomendado)

1. **Arrancar con Option A** para tener producto funcionando en semanas y cumplir el 15-nov con los clientes que ya estén listos (o que usen el Facturador Gratuito mientras tanto).
2. Los **3 primeros clientes** se certifican como emisores electrónicos usando el PSFE → satisfacen el requisito de los "3 contribuyentes certificados".
3. Con esa base, **solicitamos la certificación como PSFE** ante la DGII.
4. Cuando Volterp sea PSFE, **el transporte se mueve a casa** (Option B) y la API interna de Volterp queda estable: el contrato con el cliente no cambia, solo cambia quién firma y quién habla con la DGII.

**Decisión**: Option A. La fase 0 (dominio fiscal) es idéntica en las tres opciones, así que se construye igual; lo que cambia es el transporte (fase 3), que queda detrás de una interfaz propia.

> Nota: existe `dgii-ecf` en open source (npm) con firma y envío directo, útil como referencia de la integración propia.

## What Nurbansoft already did (reference implementation, not on disk)

El repo de Nurbansoft ya no está en la máquina; queda en memoria (#144–#152). Sirve de blueprint:

- **Dos capas separadas**: `NurbanSoft.DgiiEcif` (OUTBOUND) + `NurbanSoft.BussinesLogic` (INBOUND).
- `DgiiRecepcionController` (133 líneas) con 5 endpoints de los web services de la DGII:
  `GET /fe/autenticacion/api/semilla` (XML `<Semilla><Valor/><Fecha/></Semilla>`, no JSON) ·
  `POST /fe/autenticacion/api/validacioncertificado` ·
  `POST /fe/recepcion/api/ecf` ·
  `POST /fe/aprobacioncomercial/api/ecf` ·
  `POST /fe/envio/api/ecf`.
- Tablas: `AuthenticationSeed`, `EcfReceived`, `EcfCommercialApproval` (RNC en `NVARCHAR(500)` por valores encriptados).
- US **"Firma XML DGII"** quedó solo **diseñada, no implementada**. El diseño ya tenía lo correcto: certificado por `CustomerId`, automático y sin selección manual; validar existe / activo / tiene llave privada / no expirado / válido para firma / corresponde al customer; **nunca** exponer password, ruta ni llave privada; firma `XMLDSig` + `RSA-SHA256` + `Digest SHA256` + `Enveloped Signature`; historial completo y auditoría.

## Domain gaps in Volterp (verified in code)

| Gap | Impacto fiscal |
|---|---|
| `Client` no tiene `TaxId`/RNC | **Bloqueante**: E31 exige el RNC del comprador |
| `Company.TaxId` valida 8–15 alfanuméricos | No es validación de RNC real (9 dígitos de persona jurídica o 11 de cédula) |
| `Sale` sin NCF, sin `Itbis`, sin desglose de taxes, sin flag "consumidor final" | No se puede construir el e-CF |
| `Sale.Total` es un único `decimal` | El e-CF exige subtotal + ITBIS + total |
| `Product` sin código de impuesto (gravado 18% / exento) | El detalle de la línea lo exige |
| `SaleStatus` = Pending / Completed / Cancelled | Falta el estado fiscal (emitido, aceptado, rechazado) |
| No existe entidad e-CF, ni certificado, ni secuencia de NCF | No hay dónde persistir el comprobante fiscal |
| `System.Security.Cryptography.Xml` 9.0.0 (NU1903 HIGH) y `Pkcs` 9.0.0 (NU1902/NU1903) | Son exactamente los paquetes del firmador XMLDSig |

### Multi-tenant tie-in

Toda entidad nueva de este dominio —certificado, e-CF, secuencia, recepción— es **tenant-scoped por definición**: un certificado pertenece a una empresa, un e-CF pertenece a una empresa. Con el mecanismo ya implementado (`ITenantScoped` + `TenantScopeGuard`), esto se declara en la entidad y el guard falla el arranque si alguien se olvida. **No es un extra: es el mismo trabajo de las entidades, sin excepciones.**

## Specs

- **S1** — `Client` debe llevar RNC validado con el formato real de la DGII: **9 dígitos** para persona jurídica (RNC con dígito verificador módulo 11, pesos 7-9-8-6-5-4-3-2) o **11 dígitos** para persona física (cédula, dígito verificador Luhn, más las cédulas históricas de la JCE que no pasan Luhn y siguen siendo válidas). El `ValueObject` anterior validaba 8–15 alfanuméricos y no servía.
- **S2** — `Company` (emisor) debe llevar RNC con la misma validación, más el nombre legal y la dirección fiscal que exige el e-CF.
- **S3** — `Product` debe declarar su código de impuesto según el catálogo de la DGII (`<IndicadorFacturacion>` del "Formato Comprobante Fiscal Electrónico (e-CF) v1.0"): **0** No facturable, **1** ITBIS tasa 1 (18%), **2** ITBIS tasa 2 (16%), **3** ITBIS tasa 3 (0%), **4** Exento. Sin esto la línea del comprobante no es válida. El valor persistido es el código de la DGII; la tasa de cada código la aplica el cálculo de la venta (T4).
- **S4** — `Sale` debe desglosar el monto: `Subtotal`, `Itbis`, `Total`. El `Total` actual es un único decimal. El desglose lo calcula el dominio a partir de las líneas y del código de impuesto de cada una (tasa 18% / 16% / 0%); el `Total` que envía el cliente no es la autoridad.
- **S5** — `Sale` debe llevar el tipo de comprobante (E31 crédito fiscal / E32 consumo) y, en E31, el RNC del receptor. El RNC del receptor es una foto del `Client.TaxId` del momento de la venta, y una venta de crédito fiscal no se puede completar sin él.
- **S6** — El sistema debe asignar y controlar la **secuencia de e-NCF por tipo y por emisor**, con control de concurrencia. El e-NCF es `E` + tipo + 10 dígitos, y los rangos no usados se anulan ante la DGII.
- **S7** — Debe existir una entidad de **certificado del tenant**: `.p12`, metadata (nombre, RNC, fecha de expiración, estado) y la credencial de acceso al emisor. El material sensible **nunca** sale por la API: ni password, ni ruta, ni llave privada, ni contenido del `.p12`.
- **S8** — Debe existir una entidad **e-CF** que persista el comprobante: `eNCF`, tipo, RNC emisor, RNC receptor, XML firmado, TrackId, código de seguridad, QR, fecha de firma, estado y el motivo de rechazo de la DGII.
- **S9** — El estado del e-CF debe modelar el ciclo real: `Draft → Firmado → Enviado → Aceptado | Rechazado`, con reintento y el motivo de rechazo persistido.
- **S10** — Debe existir una entidad de **recepción**: e-CF recibidos de terceros (vía RGCE o push de la DGII) y **aprobación comercial** emitida por nosotros.
- **S11** — Toda entidad nueva de este dominio implementa `ITenantScoped`. El guard de arranque es la garantía, no una convención.
- **S12** — Auditoría: quién generó, quién firmó, con qué certificado, cuándo, y el historial de envíos y respuestas de la DGII.
- **S13** — Los paquetes criptográficos con vulnerabilidad conocida (`System.Security.Cryptography.Xml`, `Pkcs`) se actualizan **antes** de escribir cualquier código de firma.
- **S14** — La capa de transporte (web services de la DGII o API del PSFE) queda detrás de una interfaz propia, para que cambiar de Option A a Option B no toque ni dominio ni controllers.

## Phases

### Fase 0 — Dominio fiscal (sin dependencia de la DGII)

El trabajo que le da valor al producto aunque la integración se demore: Volterp sabe construir el comprobante fiscal.

| Task | Specs | Ruta | Commit | Notes |
|---|---|---|---|---|
| T1 | S13 | inline | `a07d51d` | `Cryptography.Xml` y `Pkcs` a 10.0.14, `SQLitePCLRaw.lib.e_sqlite3` a 2.1.14. 0 paquetes vulnerables en los 5 proyectos; 292/292 tests. |
| T2a | S1 | inline | `cf24711` | VO `Rnc` + `RncType` + `InvalidRncException`: dígito verificador real, normalización de guiones, 33 cédulas históricas. 295/295 tests. |
| T2b | S1 | inline | `c9cac9c` | `Client.TaxId` (columna nullable: el consumidor final del tipo 32 no tiene) + validación y normalización en `ClientService`. 300/300 tests. |
| T2c | S2 | inline | `22536df` | Validación y normalización del RNC del emisor en `CompanyService`. 302/302 tests. |
| T3 | S3 | inline | `ba2e1c7` | `TaxCode` enum con los valores = códigos `<IndicadorFacturacion>` de la DGII (0..4), persistido como int. 273/273 tests. |
| T4 | S4, S5 | inline | `eab8081` | `Sale.Subtotal`/`Itbis`/`Total` calculados por el dominio desde las líneas y su código de impuesto; `SaleDocumentType` (E31/E32) con el valor = código DGII; `Sale.ReceptorTaxId` como foto del `Client.TaxId`; una venta de crédito fiscal no se completa sin RNC del receptor. 285/285 tests. |
| T5 | S6 | inline | — | Entidad de secuencia de e-NCF con control de concurrencia. Pendiente. |
| T6 | S11 | inline | — | Todas las entidades nuevas con `ITenantScoped`. Pendiente. |

### Fase 1 — Certificado y credenciales del emisor

| Task | Specs | Notas |
|---|---|---|
| T7 | S7 | Entidad de certificado + credencial, cifrado en reposo, metadata pública separada del material sensible. |
| T8 | S7 | Alta del tenant en el PSFE (o en la DGII si vamos por Option B): cuenta, `.p12`, API key. |
| T9 | S7 | Tests: el material sensible nunca aparece en ninguna respuesta de la API. |

### Fase 2 — Generación y firma

| Task | Specs | Notas |
|---|---|---|
| T10 | S8, S9 | Entidad e-CF + máquina de estados fiscal. |
| T11 | S8, S12 | Generación del payload e-CF desde una `Sale` + historial. |
| T12 | S8 | Firma. Con Option A, la firma la hace el PSFE; con Option B, `XmlDsig` + RSA-SHA256 + Enveloped Signature. |
| T13 | S9 | Envío, idempotencia y manejo de rechazo de la DGII. |

### Fase 3 — Transporte

| Task | Specs | Notas |
|---|---|---|
| T14 | S14 | Interfaz de transporte + implementación contra el PSFE (Option A). |
| T15 | S10 | Recepción de e-CF de terceros y aprobación comercial. |
| T16 | S12 | Webhooks / polling de estado. |

### Fase 4 — Certificación PSFE — **fuera de alcance**

**Decisión del usuario (2026-11-03)**: Volterp no se certifica como PSFE, así que esta fase no se planifica. Si algún día se reconsidera, es un track regulatorio aparte que no bloquea las fases 0–3.

## Risks

| Riesgo | Impacto | Mitigación |
|---|---|---|
| **Plazo del 15-nov-2026** | 12 días. Ninguna fase completa entra en ese plazo | Fase 0 no depende de la DGII. Los clientes que no lleguen a tiempo usan el Facturador Gratuito; Volterp entra después con el ERP yaConstruido |
| **Guardar llave privada del cliente** | Un leak = la identidad fiscal de un cliente, comprometida | Option A: el `.p12` vive cifrado en el PSFE (AES-256), nosotros solo guardamos la API key. Option B: cifrado por tenant con clave en KMS/HSM + rotación |
| **No certificarnos como PSFE a tiempo** | Volterp no puede emitir en nombre de clientes | Option A como puente; bootstrap de los 3 contribuyentes para el PSFE propio |
| **Servicios de un tercero** | Dependencia de precio y de disponibilidad del PSFE | La interfaz de transporte (S14) aisla la decisión. El producto no depende del proveedor, la implementación sí |
| **Numeração de NCF mal gestionada** | Secuencias duplicadas = comprobantes fiscales inválidos | Secuencia por tipo y emisor con control de concurrencia y prueba de unicidad |
| **Certificado expirado** | El cliente deja de poder emitir sin avisar | Alerta de vencimiento con antelación; el estado del certificado bloquea el envío con mensaje claro |

## Log

- **L1** (2026-11-03, verbatim): "Champ recuerdas la informacion de como implementamos la facturacion electronica en Nurbansoft, quiero que lo hagamos en Volterp y que investiguemos como podemos hacerlo nosotros por el momento siendo emisores informales, ya que los clientes pueden ser ellos los certificados y que puedan emitir sus ecf o comprabantes fiscales pero por medio de nuestra api o sistema. pero antes de hacer eso validame en que punto estamos con la implementacion del multi-tenant de este sistema si esta completo o nos falta algo en base a lo que ya hemos trabajado en memoria tenemos mucha informacion"
- **L2** (2026-11-03): Se cierra el multi-tenant (Gap 1 + fail closed) y se mergea a `development`. El usuario pide "vamos hacer el plan para la facturacion electronica" y crear la branch `feature/electronic-invoicing`.
- **L3** (2026-11-03): Investigación contra la documentación oficial de la DGII y la API de un PSFE certificado. Se agrega el eje build-vs-integrate al plan: el modelo pedido por el usuario (certificado del cliente, firmado por nuestra API) es exactamente el de un PSFE, y existe uno ya certificado que lo soporta en modo multi-tenant.
- **L4** (2026-11-03): Se ejecuta T1 y T2a. La investigación contra la **Guía 2 del RNC de la DGII** y contra el validador de referencia `hminaya/SDQ` **corrige S1**: el RNC de persona jurídica tiene **9 dígitos** y el de persona física es la **cédula de 11 dígitos**. **No existe un RNC de 13 dígitos** — ese "13" es el largo del e-NCF (`E` + 2 de tipo + 10 de secuencia). La validación de persona jurídica usa módulo 11 con pesos 7-9-8-6-5-4-3-2 y los restos 0 y 1 se resuelven como 2 y 1; la de persona física usa Luhn, **más 33 cédulas históricas de la JCE** que no pasan Luhn y siguen siendo válidas. También queda confirmado el catálogo de los **10 tipos de e-CF** y que solo **31, 33 y 45** exigen RNC del comprador, y que el **32 es el único con RFCE** (montos < RD$250,000).

- **L5** (2026-11-03): Se cierra T2. `Client` ya carga su RNC/cédula y `Company` valida el RNC del emisor en cada escritura. El valor se **normaliza a dígitos** antes de persistir: los guiones son solo de presentación, y normalizarlos evita dos filas con el mismo RNC bajo el índice único. Se conservó `string` en las entidades (igual que `Company.Email`) en vez de un VO tipado, porque `Company.TaxId` ya tiene filas persistidas con valores que no son RNC y una migración de datos sería destructiva; la validación vive en el service, que es donde el template pide que viva (`Ensure` + `TryParse`).

- **L6** (2026-11-03): El usuario fija la directiva de calidad del suite: "debemos ser muy meticulosos con los test: calidad ante cantidad". Criterio aplicado: un test sobrevive solo si puede fallar por una razón que le importa al producto; se elimina lo que solo afirma que un setter de una línea guardó lo que se le pasó, lo que re-verifica la herencia de una excepción y lo que re-verifica el lenguaje. Resultado: **302 → 265 tests**, 8 archivos, `b8d4d57` (`test: prune low-value assertions`), 265/265 verdes. Se conservaron los tests que sí fijan reglas: la matriz de transición de roles de `User.ChangeRole`, las guardas de stock de `Product`, los overrides de `Money` con distinta moneda, la igualdad de `Currency` (que el compilador no puede garantizar al ser un `int` persistido) y los 5 casos de valor de `Rnc`. Dos hallazgos colaterales quedan abiertos y **no** se tocaron por estar fuera de alcance: `Client.Activate()`/`Deactivate()` parecen código muerto (el `ClientService` asigna `IsActive` directamente), y `PurchaseService`/`SaleService` devuelven `Right(null)` en not-found mientras `CompanyService` devuelve `Left(error)` — inconsistencia de contrato entre servicios.

- **L7** (2026-11-03, verbatim): "aunque enfatizo no voy a certificarme yo de momento quiero que los clientes pueden emitir comprobantes fiscales electronicos por medio de mi sistema" — **queda decidido Option A**: Volterp no se certifica como PSFE; los clientes certifican su propio emisor con su `.p12` a través del PSFE y emiten desde la API de Volterp. El modelo fiscal (Fase 0) no cambia; el transporte de la Fase 3 se implementa contra el PSFE detrás de una interfaz propia. En la misma corrida se **corrige S3** contra el catálogo oficial (`<IndicadorFacturacion>` del "Formato e-CF v1.0"): hay **tres** tasas de ITBIS (18%, 16%, 0%) y no una sola; el 0% es la tasa 3 y no un "no gravado"; y hay una quinta categoría, "No facturable". Implementar solo el 18% habría generado e-CF inválidos. T3 cierra con el enum `TaxCode` (`ba2e1c7`).

- **L8** (2026-11-03, verbatim): "vamos a ver si respetas el codigo no inventes cosas no me ensucies mi codigo debe seguir la misma forma" y "no vamos a crear codigo simplemente para rellenar huecos / aqui validamos y refactorizamos y vemos que podemos extender y reutilizar". **Regla de trabajo para el resto del plan**: antes de escribir un tipo nuevo, validar primero qué existe y si se puede extender o reutilizar. Aplicado en T3: se descartó un `TaxCode` como VO (con su excepción y su `ValueConverter`) porque el dominio ya tiene la forma para esto — las 6 clasificaciones son enums (`Currency`, `UserRole`, `SaleStatus`, `TransactionType`, `EntityStatus`, `RncType`) y los VOs (`Email`, `Money`, `Rnc`) son para valores con formato o aritmética. La diferencia que decide: `Rnc` es un valor arbitrario que hay que **verificar** con un algoritmo (módulo 11 / Luhn) y por eso es VO; `TaxCode` es un valor de una lista **cerrada** de 5 códigos y por eso es enum. También se descartó un `Enum.IsDefined` en el service: no hay ningún enum validado así en el repo y el `JsonStringEnumConverter` ya rechaza los valores desconocidos en el borde de la API.

- **L9** (2026-11-03): T4 cierra el desglose fiscal de la venta (`eab8081`). Todas las decisiones de forma se validaron contra el repo antes de escribir (la regla de L8), no después:
  - **Los montos siguen en `decimal`, no en `Money`.** `Product.Price` sí es `Money`, pero el VO guarda la moneda *dentro* del valor: tres montos la duplicarían en seis columnas. El desglose fiscal de la venta es hoy en DOP y la moneda del comprobante es un tema de la Fase 2 (payload e-CF), no de S4.
  - **La tasa vive en el dominio**, no en el service: `Sale.TaxRate` es un `switch` exhaustivo sobre `TaxCode` (18% / 16% / 0% / exento / no facturable). Como el enum no tiene un valor 5, el compilador avisa si mañana se agrega una tasa sin definir, y un valor desconocido lanza en vez de tributar en 0%.
  - **El código de impuesto de la línea sale del producto**, no del request — el producto es la autoridad — y queda *snapshot* en `SaleItem.TaxCode`: el comprobante refleja el momento de la venta, no el precio de catálogo de mañana. Una línea cuyo producto no existe se declara **no facturable** en vez de inventarle una tasa.
  - **El RNC del receptor sale del `Client.TaxId`** (ya validado con el VO `Rnc` en T2b) y queda *snapshot* en `Sale.ReceptorTaxId`.
  - **El invariante E31 se hace con `InvalidSaleStateTransitionException`**, la excepción que el repo ya usa para "must have at least one item": ninguna excepción nueva, ninguna forma nueva.
  - **El servidor pasó a ser la autoridad del total.** Antes `CreateSaleRequest.Total` se persistía tal cual y `SaleService.Update` sobrescribía el total con el del request; ahora el dominio lo calcula y el cliente ya no puede producir una venta cuyo total no cuadre con sus líneas — que es la condición de validez de un e-CF.
  - **Migración**: EF genera `defaultValue: 0` en columnas int no-null, y 0 es "No facturable" y un tipo de comprobante inválido → corregidos a mano (`DocumentType` → 32, `TaxCode` → 1) y agregado el backfill `Subtotal = Total` para las ventas previas, que no tenían ITBIS.
  - **Verificación**: 285/285 tests. Los 12 tests nuevos se validaron además **rompiendo la implementación a propósito** (tasa 16% → 0%, invariante E31 desactivado, RNC del receptor sin resolver): cayeron exactamente los 5 tests que deben caer, y ningún otro. La migración no se pudo aplicar contra un Postgres real (no hay Docker ni base de datos levantada en la máquina), igual que la de T3.

## Relevant Files

- `Volterp.Domain/Entities/{Client,Company,Product,Sale}.cs` — modelo fiscal a construir (Fase 0)
- `Volterp.Domain/ValueObjects/Rnc.cs` — VO con el dígito verificador real de la DGII
- `Volterp.Domain/Enums/TaxCode.cs` — código de impuesto DGII por ítem (`IndicadorFacturacion` 0..4)
- `Volterp.Domain/Enums/SaleDocumentType.cs` — tipo de comprobante de la venta (E31 crédito fiscal / E32 consumo)
- `Volterp.Domain/Abstractions/ITenantScoped.cs` — declaración de tenant scope de las entidades nuevas
- `Volterp.Infrastructure/Data/TenantScopeGuard.cs` — falla el arranque si una entidad con `CompanyId` no declara `ITenantScoped`
- `Volterp.Application/Services/SaleService.cs` — `CompleteSaleAsync` es hoy el punto donde el e-CF se emitiría; el desglose fiscal se calcula en `Sale.CalculateTotals`
- `Volterp.Application/Services/ClientService.cs` — alta y persistencia del RNC del receptor
- `odd/tasks/multi-tenant-isolation.md` — precedente de tracking doc de ODD en este repo
