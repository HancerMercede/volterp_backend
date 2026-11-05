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

**Revisado (2026-11-04): el modelo propio de tu colega — "nosotros la comunicación, el cliente su firma y sus credenciales" — es el que vamos a construir.** Option A (integrarse con la API de un PSFE) **deja de ser el camino de transporte** y pasa a quedar solo como registro de lo evaluado. Ver "El modelo confirmado" abajo y L14.

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

**Decisión (2026-11-03, confirmada por el usuario)**: **Option A ahora, nuestra propia implementación después.** El plan de fases 0–3 se construye sobre Option A (eCF MSeller como PSFE). La certificación de Volterp como PSFE pasa de "fuera de alcance" a **segunda etapa, disparada por tener clientes emisores**; el contrato con el cliente no cambia porque el transporte vive detrás de S14. La fase 0 (dominio fiscal) es idéntica en las tres opciones, así que se construye igual; lo que cambia es el transporte (fase 3), que queda detrás de una interfaz propia.

> Nota: existe `dgii-ecf` en open source (npm) con firma y envío directo, útil como referencia de la integración propia.

## El modelo confirmado (2026-11-04) — nosotros la comunicación, el cliente su firma

**Es lo que hace el colega del usuario, y es el modelo que se va a construir.** Cada cliente es un **emisor electrónico** que se certifica ante la DGII por la vía de **sistema de desarrollo propio**: conserva sus **credenciales de la DGII** (usuario administrador de e-CF) y su **certificado digital**, y nosotros nos limitamos a **hablar con la DGII** y a **firmar con el certificado del cliente**.

Evidencia que lo respalda (verificada contra la documentación de dos proveedores, #1821): en el **Portal de Certificación de la DGII**, el contribuyente registra el software que usará como **"Tipo EXTERNO"** con los datos del proveedor (RNC, razón social, nombre comercial) y **las URLs del proveedor** para **recepción** y **aprobación comercial**. Esas URLs serían las de nuestra API. O sea: **el modelo de nuestro colega es el que la DGII tiene previsto para un software que se registra como proveedor externo** — no es un atajo ni una excepción.

Lo que esto cambia en el plan:

| | Plan anterior (Option A) | Modelo confirmado |
|---|---|---|
| Transporte | API HTTP de un PSFE (eCF MSeller) | **Web services de la DGII**, directos |
| Quién firma | El PSFE, con el `.p12` del cliente | **Nosotros, con el `.p12` del cliente** |
| Custodia del `.p12` | Cifrado en el PSFE | **Cifrado en nuestro Postgres** (T7 ya resuelve esto) |
| URLs de recepción/aprobación | Las del PSFE | **Las de nuestra API** (la DGII nos llama) |
| T12 (firma) | Opcional, la hacía el PSFE | **Núcleo y primera** |
| T14 (transporte) | Cliente HTTP de MSeller | **Cliente de los web services de la DGII** |
| Fase 4 (certificar PSFE) | Segunda etapa | **Por confirmar** (ver la pregunta abierta abajo) |

**Dos consecuencias que no estaban en el plan y hay que absorber:**

1. **Nuestra API pasa a ser superficie pública que la DGII consume.** Con el modelo confirmado, el cliente registra **nuestras URLs** de recepción y aprobación comercial en su certificación → **T15 deja de ser una fase tardía y entra en el camino crítico**: la certificación del cliente depende de que esos endpoints ya existan y funcionen. Esto también cambia la postura de seguridad de nuestra API (la DGII entra a ella).
2. **S7 necesita una segunda credencial.** Hoy la entidad guarda **un** valor cifrado opaco. Con este modelo el emisor tiene **dos** secretos: el **certificado de firma** (`.p12` + password) y la **credencial de usuario administrador de e-CF**, que es con la que se pide la **semilla** que autentica las llamadas a los web services. Son credenciales distintas con ciclo de vida distinto.

**Lo que ya está construido y no se tira a la basura**: T7 (`CompanyCertificate`, metadata pública + credencial cifrada en reposo con AES-256-GCM y clave derivada por tenant) es **exactamente** la base para custodiar el `.p12` del cliente. Bajo Option A la credencial cifrada era la API key del PSFE; bajo este modelo es el certificado del cliente. **La entidad no cambia** — ese era el propósito de haberla designed como valor opaco desde el inicio.

**Pregunta abierta, con dueño: la certificación PSFE.** Todo el planteo de "nosotros firmamos por varios contribuyentes" choca de frente con el requisito de la DGII de certificarse como PSFE (≥3 contribuyentes ya certificados como emisores electrónicos, actividad de software en el RNC, al día con tributación, solicitud con anexos). El colega del usuario opera este modelo para sus clientes, así que **la pregunta no es理论ica**: hay que confirmarle **a él** si se certificó como PSFE o si sus clientes lo registran como proveedor "Tipo EXTERNO" **sin** que él tenga certificación PSFE. Esa respuesta decide si la Fase 4 existe. `dgii.gov.do` sigue inaccesible desde esta máquina, así que no se puede cerrar desde la documentación.

## Arquitectura — la library portable (2026-11-04)

**Decisión del usuario (2026-11-04, verbatim)**: "la arquitectura la vamos a hacer como un proyecto separado un class lib aparte para que si en un futuro queremos vender el servicio a terceros lo puedan consumir" y "pero dentro de la misma solucion".

**Qué es**: un proyecto de class library nuevo, `Volterp.ElectronicInvoicing`, **dentro de `Volterp.slnx`** y con su propio proyecto de tests (`Volterp.ElectronicInvoicing.Tests`).

**La regla que la hace vendible**: la library **no referencia ningún otro proyecto de Volterp** — ni Domain, ni Application, ni Infrastructure, ni Api. Cero dependencias de ERP. Un tercero que quiera emitir e-CF referencia **solo** esta library, construye el documento en términos DGII y firma. Sin arrastrar nuestro dominio de ventas, productos ni clientes.

**Qué vive adentro** (todo lo que es *protocolo* y no *ERP*):

| En la library | Por qué |
|---|---|
| `EcfDocumentType` (los 10 tipos oficiales) y `TaxCode` (el `IndicadorFacturacion`) | Son catálogos **de la DGII**, no del ERP. Tenerlos en dos lados es la vía corta a un comprobante inválido |
| `IEcfSigner` + `XmlDsigEcfSigner` | La firma no depende de nuestras ventas |
| `ISigningCertificateProvider` | El puerto por el que el anfitrión entrega el `.p12` del emisor. Se indexa por **RNC emisor**, no por tenant, porque un tenant puede emitir por varias sucursales (S16) |
| (T11) `EcfXmlBuilder` | El XML del e-CF es formato de la DGII |
| (T14) transporte contra los web services de la DGII | Idem |

**Qué se queda en Volterp** (todo lo que es *persistencia* o *ERP*):

| En el ERP | Por qué |
|---|---|
| `ElectronicInvoice` (entidad e-CF) | Es persistencia con `CompanyId` → tiene que vivir donde vive el `TenantScopeGuard` |
| `CompanyCertificate` | Custodia del `.p12` cifrado, por tenant |
| `ElectronicInvoiceSequence` | Asignación atómica del e-NCF, por tenant y emisor |
| `Sale`, `Product`, `Client`, `Company` | El ERP. `Sale.DocumentType` ahora es `EcfDocumentType` y `Product.TaxCode` es `TaxCode`: **los tipos vienen de la library** |

**La dirección de dependencias**: `Volterp.Domain → Volterp.ElectronicInvoicing` (y Application, Infrastructure, Tests también la referencian explícitamente, sin apoyarse en la transitiva). La library no depende de nadie. Eso es lo que hace que un tercero pueda consumirla sin arrastrar el ERP.

**Consecuencia que hay que aceptar**: `Volterp.Domain` pasa a depender de un proyecto que también es un producto publicable. Es un precio consciente: la alternativa era duplicar los catálogos de la DGII, y esa duplicación se paga en un comprobante fiscal rechazado.

**Lo que esta arquitectura NO arregla**: el paso a producción sigue exigiendo el mismo trámite y el mismo trabajo técnico. La library cambia **dónde vive el código**, no **cuánto falta**.

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
- **S7** — Debe existir una entidad de **certificado del tenant**: metadata (nombre, RNC del emisor, fecha de expiración, estado) y la credencial de acceso al emisor, **cifrada en reposo**. El material sensible **nunca** sale por la API: ni password, ni ruta, ni llave privada, ni contenido del `.p12`. La credencial se guarda como **valor cifrado opaco**, de modo que la decisión estratégica no obliga a rehacer la entidad. **Corregido (2026-11-04, modelo confirmado)**: el `.p12` **sí** se persiste en Volterp — es lo que el emisor nos entrega para que firmemos — y por eso la credencial cifrada es el certificado del cliente, no una API key de un tercero. **Además hace falta una segunda credencial por emisor**: la del **usuario administrador de e-CF**, que es con la que se pide la semilla que autentica las llamadas a los web services. Son dos secretos con ciclo de vida distinto, y la entidad actual tiene un solo campo `EncryptedCredential` → **S7 queda por refinar** (un campo, dos credenciales cifradas, o dos entidades).
- **S8** — Debe existir una entidad **e-CF** que persista el comprobante: `eNCF`, tipo, RNC emisor, RNC receptor, XML firmado, TrackId, código de seguridad, QR, fecha de firma, estado y el motivo de rechazo de la DGII. **Ampliado (2026-11-04, Guía 6)**: el **QR** no es una imagen genérica — codifica un **URL de verificación** que lleva estos datos: RNC emisor, razón social emisor, RNC comprador, razón social comprador, e-NCF, fecha de emisión, total de ITBIS, monto total y estado. Y el **código de seguridad** (7 caracteres, p. ej. `WkT3Sa`) no es decorativo: la DGII verifica el e-CF con la tupla **RNC + e-NCF + RNC comprador + código de seguridad** → si el QR o el código no se generan igual que la DGII espera, el comprobante es inválido para el receptor y para la consulta pública.
- **S9** — El estado del e-CF debe modelar el ciclo real: `Draft → Firmado → Enviado → Aceptado | Rechazado`, con reintento y el motivo de rechazo persistido. **Corregido (2026-11-04, contra la "Guía 6 Facturación Electrónica" de la DGII, descargada y leída)**: la DGII define **cinco** estados de veredicto, no dos — **Aceptado**, **Rechazado**, **Aceptado condicional** ("el e-CF tiene validez para fines fiscales, pero existe una irregularidad que no amerita el rechazo, pero que debe observarse y/o corregirse para la emisión de futuros comprobantes"), **En proceso** (la DGII está validando: hay que consultar de nuevo) y **Anulado** (remitido por el contribuyente en el formato de anulación). Our tres estados internos (`Draft`/`Firmado`/`Enviado`) son previos al veredicto; los cinco de la DGII son el resultado. **"Aceptado condicional" no puede reducirse a "Aceptado"**: es el estado que más traga en un ERP, porque hay que corregir la causa raíz antes de la siguiente emisión.
- **S10** — Debe existir una entidad de **recepción**: e-CF recibidos de terceros (vía RGCE o push de la DGII) y **aprobación comercial** emitida por nosotros. **Corregido (2026-11-04, modelo confirmado)**: esto no es solo una entidad interna — el cliente registra **nuestras URLs** de recepción y de aprobación comercial en su certificación ante la DGII, así que **nuestra API expone endpoints públicos que la DGII consume**. Eso los pone en el **camino crítico** de la certificación del cliente y cambia la postura de seguridad de la API (la DGII entra a ella).
- **S11** — Toda entidad nueva de este dominio implementa `ITenantScoped`. El guard de arranque es la garantía, no una convención.
- **S12** — Auditoría: quién generó, quién firmó, con qué certificado, cuándo, y el historial de envíos y respuestas de la DGII.
- **S13** — Los paquetes criptográficos con vulnerabilidad conocida (`System.Security.Cryptography.Xml`, `Pkcs`) se actualizan **antes** de escribir cualquier código de firma.
- **S14** — La capa de transporte (web services de la DGII o API del PSFE) queda detrás de una interfaz propia, para que cambiar de Option A a Option B no toque ni dominio ni controllers.
- **S15** — Debe existir el tipo de comprobante **nota de crédito** (**E34**; E33 es la nota de débito — confirmado el 2026-11-03 contra el set de pruebas de la certificación de dos proveedores independientes). Una venta ya emitida **no se edita ni se borra**: se corrige con nota. Sin esto el ERP no tiene salida fiscal para una venta anulada.
- **S16** — Un tenant puede tener **varios RNC emisores** (sucursales). El certificado pertenece al **RNC emisor**, no al tenant; por eso la entidad guarda el RNC como snapshot, igual que `Sale.ReceptorTaxId` es el snapshot del `Client.TaxId`.
- **S17** — Debe existir la **Representación Impresa (RI)** del e-CF. **Nuevo (2026-11-04, Guía 6 de la DGII)**: el receptor **no electrónico** recibe la RI en papel, y la RI cumple el Decreto 254-06 y la Norma General 06-2018. La RI **no es una copia del XML**: es un documento con layout propio que incluye RNC y razón social del emisor, dirección, fecha de emisión y de vencimiento, tipo de comprobante con el e-NCF, RNC y razón social del cliente, la tabla de líneas (cantidad, descripción, unidad de medida, precio, ITBIS, valor), Subtotal Gravado, Total ITBIS, Total, **Código de Seguridad**, **Fecha de Firma Digital** y el **QR**. **Impacto de producto**: el tipo 32 (consumo) no exige RNC del comprador, así que su receptor es típicamente no electrónico → **toda venta de consumo necesita una RI imprimible**, y hoy no hay nada que la genere.

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
| T5 | S6 | inline | `5bff9fa` | `ElectronicInvoiceSequence` por (emisor, tipo) con índice único; la asignación es **una sola sentencia atómica** (`INSERT … ON CONFLICT DO UPDATE … RETURNING`), verificada con 25 llamadas concurrentes contra Postgres real. 291/291 tests. |
| T6 | S11 | inline | `5bff9fa` | La entidad de T5 declara `ITenantScoped` y el `TenantScopeGuard` falla el arranque si alguna entidad con `CompanyId` no lo declara: el requisito queda garantizado por el guard, no por la convención. Las entidades de la Fase 1 y 2 quedan cubiertas por el mismo guard. |

### Fase 1 — Certificado y credenciales del emisor

| Task | Specs | Ruta | Commit | Notas |
|---|---|---|---|---|
| T7 | S7 | inline | `6261501` + `622341c` | Entidad `CompanyCertificate` + credencial cifrada en reposo (AES-256-GCM, clave derivada por tenant), metadata pública separada del material sensible. 625 líneas escritas en dos commits: `6261501` núcleo + `622341c` API. 312/312 tests. |
| T8 | S7 | — | — | **Cambiado (2026-11-04)**: alta del emisor ante la DGII por la vía de **sistema de desarrollo propio**. El cliente se certifica como emisor electrónico en el Portal de Certificación de la DGII registrándonos como software **"Tipo EXTERNO"** con nuestros datos y **nuestras URLs de recepción y aprobación comercial**; el cliente nos entrega su `.p12` + password y su credencial de usuario administrador de e-CF. No es un endpoint: es un proceso + acuerdo comercial. **Depende de T14 y T15 vivos** (el cliente registra URLs que tienen que funcionar). |
| T9 | S7 | inline | `714117f` | **Cerrado.** Tests: el material sensible nunca aparece en ninguna respuesta de la API. La fila quedó desactualizada: el chequeo de la respuesta HTTP completa ya existe en `CompanyCertificateIntegrationTests` — 6 tests de integración prueban que el body HTTP no lleva ni el texto claro ni el ciphertext, que la fila guardada sí queda cifrada, que otro tenant no puede ni leer ni listar el certificado y que un no-admin no puede crearlo. |
| T17 | S15 | inline | `28ee400` | **Resuelto.** El enum `SaleDocumentType` se eliminó y lo sustituyó `EcfDocumentType` en la library, que trae **los 10 tipos oficiales** (31, 32, 33, 34, 41, 43, 44, 45, 46, 47) → **la nota de crédito E34 ya está en el catálogo**, sin trabajo extra. La venta se corrige con nota, no editando. |
| T18 | S16 | — | — | Multi-RNC por tenant: el certificado pertenece al RNC emisor; `Company` deja de tener un solo RNC implícito. |

### Fase 2 — Generación y firma

| Task | Specs | Ruta | Commit | Notas |
|---|---|---|---|---|
| T10 | S8, S9 | inline | `cd41f9c` | **Cerrado.** Entidad `ElectronicInvoice` + máquina de estados con los **5 veredictos de la DGII** (Aceptado, Rechazado, Aceptado condicional, En proceso, Anulado), no 2. 22 tests nuevos, 334/334 + 10/10. Migración `20261104163914_AddElectronicInvoices` **aplicada y verificada contra Postgres real**: índice único `(IssuerTaxId, ENCF)`, FKs `ON DELETE RESTRICT`, `DocumentType`/`Status` sin default 0. |
| T20 | S14 | inline | `28ee400` | **Cerrado.** `Volterp.ElectronicInvoicing` + `Volterp.ElectronicInvoicing.Tests`: la class library **vendible por separado**, sin referenciar ningún otro proyecto de Volterp. Los catálogos de la DGII (`TaxCode`, `EcfDocumentType` con los 10 tipos oficiales) se mudaron a la library y `SaleDocumentType` se eliminó → **T17 resuelto de regalo**. 39 archivos, +553/−61. |
| T11 | S8, S12 | inline | `49e159a`, `2c0beb3`, `da65e34`, `71f808d` | **Cerrado.** El XML oficial ya no se deduce del PDF: la DGII publica los **XSD** (`Documentaci%C3%B3n%20T%C3%A9cnica%20(XSD)`), y el de cada tipo dice la **raíz (`<ECF>`), el orden exacto de las áreas, cuáles campos existen, cuáles son obligatorios y con qué formato**. 10 XSD copiados al repo como `Volterp.ElectronicInvoicing/Schemas/ecf-NN.xsd` y **embebidos** en la library. La library **escribe el XML y lo valida contra el esquema oficial antes de devolverlo** (fail-closed): `EcfXmlWriter` (dinámico por tipo, un solo camino de código) + `EcfSchema` (introspección del XSD: qué admite y qué exige cada tipo) + modelo neutral en `Volterp.ElectronicInvoicing/Xml/`. **T11b (el mapeo que faltaba)**: `EcfTaxRates` (tabla única de tasas por `TaxCode`), `EcfTotalsCalculator` (totales por tasa, redondeo por línea, buckets vacíos en `null`, lo no facturable fuera del total), `EcfTypeProfiles` (helper por tipo derivado del `minOccurs` del XSD) y `SaleEcfDocumentMapper` en `Volterp.Application` (fail-closed: sin líneas, sin expiry o sin tax id del receptor → `InvalidOperationException`; y **la venta debe pertenecer a la empresa que emite**). `EcfIdentity.SequenceExpiresAt` pasó a `DateTime?` y `Sale.TaxRate` delega en `EcfTaxRates.FractionOf`. 73/73 + 346/346. **Bug real corregido**: el writer anidaba `<TelefonoEmisor>` dentro de sí mismo (XML inválido). |
| T12 | S8 | inline | `28ee400` | **Cerrado y VALIDADO contra la spec oficial.** `XmlDsig` + RSA-SHA256 + Enveloped Signature + Digest SHA256 + **C14N inclusiva** + `KeyInfo/X509Data`, **fail-closed** (verifica antes de devolver). Coincide **línea por línea con el C# de referencia de la propia DGII** ("Firmado de e-CF"). La spec dice que **SHA256 es obligatorio**. 10/10 tests + los preexistentes. **Era la pieza más difícil del plan y está cerrada.** |
| T13 | S9 | — | — | Envío, idempotencia y manejo de rechazo de la DGII. |
| T19 | S17 | — | — | **Nuevo (2026-11-04)**: generación de la **Representación Impresa** del e-CF (plantilla con los datos del Decreto 254-06 y la NG 06-2018, QR y código de seguridad), para el receptor no electrónico. Es lo que recibe el cliente final de una venta de consumo. |

### Fase 3 — Transporte

| Task | Specs | Notas |
|---|---|---|
| T14 | S14 | **Cambiado (2026-11-04)**: interfaz de transporte + implementación **contra los web services de la DGII**, no contra la API de un PSFE. **URLs y flujo verificados contra la "Descripción Técnica Servicios DGII" (47 pág.)**:<br>• **Ambiente de pruebas**: `https://ecf.dgii.gov.do/testecf/...` · **Producción**: `https://ecf.dgii.gov.do/ecf/...`<br>• `GET {base}/emisorreceptor/fe/autenticacion/api/semilla` → XML `<SemillaModel><valor/><fecha/></SemillaModel>`<br>• `POST {base}/emisorreceptor/fe/autenticacion/api/validacioncertificado` (parámetro `xml` = **la semilla firmada**) → `{"token","expira","expedido"}`<br>• `POST {base}/emisorreceptor/fe/.../api/ecf` (**EmisionComprobantes**) → `{"trackId"}`<br>• `GET {base}/emisorreceptor/.../api/estado?trackid={trackId}` → el veredicto<br>• `GET {base}/consultatrackids/api/trackids/consulta?rncemisor=X&encf=Y`<br>**La autenticación es solo con el certificado**: se firma la semilla con el `.p12` y sale el token. Sin usuario ni contraseña en el protocolo. |
| T15 | S10 | **Ahora en el camino crítico**: recepción de e-CF de terceros y aprobación comercial, **expuestas como endpoints públicos de nuestra API** — el cliente las registra en su certificación (S10). Contrato verificado: `POST {base}/fe/recepcion/api/ecf` y `POST {base}/fe/aprobacioncomercial/api/ecf`. Nombre de archivo: `RNCComprador+e-NCF.xml`. |
| T16 | S12 | Webhooks / polling de estado. |

**Pendientes de verificación con la DGII (antes de T14)**:
- **Resuelto (2026-11-03) — la secuencia**: el contribuyente solicita en su Oficina Virtual el **rango** de e-NCF por tipo (`Solicitud de Comprobantes Fiscales`, ~10 minutos de aprobación) y el **proveedor asigna los números dentro del rango aprobado** → `ElectronicInvoiceSequences` (T5) sigue siendo la autoridad del número; lo que la DGII autoriza es el rango, no cada número.
- **Abierto (2026-11-04) — ¿la certificación PSFE es exigida para operar este modelo?** Ver Fase 4. Dueño: el usuario, preguntándoselo al colega que opera el modelo en producción.
- **Resuelto (2026-11-04) — la autenticación de la semilla**: el flujo documentado es **GET semilla → firmarla con el `.p12` → POST validacioncertificado → token**. La credencial es **solo el certificado**: no hace falta guardar el usuario administrador del cliente para emitir. La semilla se puede cachear; lo que se cachea es el **token**, con su `expira`.
- **Resuelto (2026-11-04) — el set de pruebas**: el "Formato Comprobante Fiscal Electrónico (e-CF) V 1.0" **ya está descargado y commiteado** como `docs/ecf-format-v1.0.md`, y la DGII **habilita secuencias de prueba en el ambiente de precertificación** (bitácora del 02-01-2026). Falta solo conseguir el **XSD** y el juego de casos de validación de la certificación, si la DGII los publica.
- **Nuevo (2026-11-04) — el `.p12` de prueba**: para una prueba end-to-end hace falta un certificado de un RNC con secuencias habilitadas en precertificación. Dueño: el usuario.

### Fase 4 — Certificación PSFE — **reabierta por el modelo confirmado (2026-11-04)**

**Decisión del usuario (2026-11-03, verbatim)**: "Ok sigamos con la opcion A por el momento luego que ya tengamos los clientes pasamos a utilizar nuestra propia implementacion".

**Revisado (2026-11-04)**: con el modelo confirmado (nosotros la comunicación, el cliente su firma y sus credenciales), Volterp hace exactamente lo que hace un PSFE — firma por varios contribuyentes. La pregunta que queda abierta es si la DGII exige o no esa certificación para operar así. **No se puede cerrar desde la documentación** (`dgii.gov.do` inaccesible desde esta máquina) y queda **pendiente de confirmar con el colega del usuario**, que opera este modelo en producción.

- **Si la DGII no lo exige** (lo que sugiere el modelo del colega): la Fase 4 desaparece del plan y no hay ningún trámite esperando. El bootstrap de los 3 contribuyentes (Option C) deja de ser necesario.
- **Si la DGII sí lo exige**: vuelve la Fase 4 con su bootstrap (3 contribuyentes ya certificados como emisores electrónicos) y su solicitud con anexos, y **eso es un camino crítico de meses** → hay que arrancarlo **en paralelo**, no al final.
- **Lo que no cambia en ningún escenario**: el trabajo técnico de T12 (firma) y T14 (transporte) es idéntico. La certificación es un **trámite**, no una dependencia de código → **se puede construir la implementación mientras el trámite avanza**, y por eso ninguna de las dos ramas bloquea el desarrollo.

**Consecuencia del modelo confirmado que el plan anterior no cubría**: el `.p12` y el password de cada cliente pasan a estar **en nuestro Postgres**, no en el PSFE. Eso reintroduce el riesgo que Option A evitaba (un leak de nuestra base = identidad fiscal del cliente comprometida) — pero **es exactamente el riesgo que T7 ya está mitigate**: credencial cifrada en reposo con AES-256-GCM, clave derivada por tenant, app que falla cerrada si la clave falta, y material sensible que nunca sale por la API. Lo que falta en T7 no es la seguridad de la credencial, es el **modelado de la segunda credencial** (S7).

## Risks

| Riesgo | Impacto | Mitigación |
|---|---|---|
| **Plazo del 15-nov-2026** | **11 días** (hoy 2026-11-04). Con el modelo confirmado las piezas pesadas (T12 firma, T14 transporte) son nuestras y **ninguna fase completa entra en el plazo** | Fase 0 no depende de la DGII. Los clientes que no lleguen a tiempo usan el Facturador Gratuito. El plazo real de Volterp hay que definirlo explícitamente, no heredarlo del plan |
| **T12 es la pieza más difícil y no hay precedente en el repo** | XMLDSig + Enveloped Signature + Digest SHA256 es donde más se puede fallar en silencio (un XML mal firmado no se ve hasta que la DGII lo rechaza) | Firmar **contra el set de pruebas de la DGII**, nunca contra producción. Nurbansoft lo diseñó y nunca lo implementó: es la trampa conocida del plan |
| **Guardar llave privada del cliente** | Un leak = la identidad fiscal de un cliente, comprometida. **Bajo el modelo confirmado el `.p12` es nuestro**, no del PSFE | Ya está mitigado por T7: AES-256-GCM, clave derivada por tenant, app que falla cerrada sin clave, material sensible que nunca sale por la API. Falta el key id para poder rotar la clave maestra en sitio |
| **Nuestra API pasa a ser superficie pública que la DGII consume** | Los endpoints de recepción y aprobación comercial los llama la DGII, no el usuario → nueva superficie de ataque y nueva postura de autenticación | Es el precio del modelo confirmado y **no es evitable**: el cliente registra nuestras URLs en su certificación. T15 tiene que diseñarse con esa amenaza desde el día uno |
| **¿Certificación PSFE exigida para operar este modelo?** | Si la DGII la exige, es un camino crítico de meses que no depende de nuestro código | Pregunta abierta con dueño (el usuario, vía su colega). **No bloquea el desarrollo**: T12 y T14 son idénticos en los dos escenarios, así que se avanza mientras el trámite se aclara |
| **Numeração de NCF mal gestionada** | Secuencias duplicadas = comprobantes fiscales inválidos | Secuencia por tipo y emisor con control de concurrencia y prueba de unicidad (T5, ya hecho) |
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

- **L10** (2026-11-03): T5 cierra la secuencia de e-NCF por tipo y emisor (`5bff9fa`). Decisiones:
  - **La fila ES la secuencia**: `ElectronicInvoiceSequences` con índice único en `(CompanyId, DocumentType)` — un emisor, un tipo, un número. Reutiliza `SaleDocumentType` ✓ (no hay enum nuevo ✓): hoy solo se emiten E31 y E32 ✓ y cuando hagan falta los demás tipos se extiende el enum ✓.
  - **La asignación es UNA sola sentencia atómica** (`INSERT … ON CONFLICT DO UPDATE … RETURNING`) y no un "leer, sumar, guardar" ✗: con dos emisores concurrentes, un leer-modificar-escribir entrega el mismo número dos veces ✗ — y un e-NCF repetido es un comprobante fiscal inválido ✗✗. El `ON CONFLICT` sobre el índice único serializa a los emisores a nivel de fila dentro de la base ✓ (Postgres bloquea la fila durante la sentencia ✓), sin reintentos en la aplicación ✓ ni contador en memoria ✓.
  - **Los huecos son correctos, no un bug**: la DGII exige anular los rangos no usados ✓, así que un e-NCF reservado y no emitido es un hueco esperado ✓ y no obliga a revertir la transacción de emisión ✓.
  - **El SQL crudo es deliberado y vive en el repositorio** (la frontera de datos ✓): EF no tiene forma de devolver el valor de un `UPDATE … RETURNING` ✓ — `ExecuteUpdate` devuelve el conteo de filas afectadas, no el nuevo valor ✗. La sentencia es una constante ✓ y filtra por `CompanyId` ✓ (la secuencia es del emisor, no del que llama ✓).
  - **Verificación**: 291/291 tests. La asignación se probó contra la base real: **25 llamadas concurrentes** (25 contextos y 25 conexiones separadas contra Postgres) devolvieron exactamente 1..25, sin repeticiones ✓. Esa prueba no se commitea porque xUnit 2.9 no tiene skip dinámico ✓ — sin Postgres en CI fallaría ✗; queda el test de integración sobre SQLite (asignación consecutiva ✓, secuencias separadas por tipo ✓ y por emisor ✓) y la evidencia de la prueba real aquí ✓. La migración se aplicó y se verificó contra Postgres ✓.

- **L11** (2026-11-03, verbatim): "Ok sigamos con la opcion A por el momento luego que ya tengamos los clientes pasamos a utilizar nuestra propia implementacion". **Rumbo re-confirmado**: Option A ahora, certificación propia después. La Fase 4 deja de ser "fuera de alcance" y pasa a **segunda etapa disparada por tener clientes emisores**. Se agregan **S15** (nota de crédito) y **S16** (multi-RNC por tenant), y las tareas **T17** y **T18** los cubren.
  - **El PSFE es un rol de la DGII, no una empresa**: con Option A lo ejerce un tercero (IT SOLUCLICK SRL con eCF MSeller, tipo EXTERNO). Ser PSFE no exime de que cada contribuyente certifique su propio emisor y reciba **su** `.p12` — son dos credenciales distintas que no se sustituyen entre sí.
  - **Consecuencia no cubierta por el plan anterior**: la transición a Option B **no es solo mover el transporte**. El `.p12` y el password de cada cliente deben pasar de la custodia del PSFE a nuestro Postgres → requiere acción y consentimiento de cada cliente → y reintroduce el riesgo que Option A evita (un leak de nuestra base = identidad fiscal del cliente comprometida). **La custodia de credenciales es una migración comercial, no un cambio de código.** Por eso S14 mantiene estable el contrato con el cliente, pero la decisión de PSFE propio tiene un costo de_CLIENTES, no solo un costo técnico.
  - **S7 se corrige** según Option A: el `.p12` **no** se persiste en Volterp (vive cifrado con AES-256 en el PSFE); nosotros guardamos la **credencial del emisor cifrada en reposo** y su metadata pública (nombre, RNC del emisor, expiración, estado).
  - **Pendiente para T8 (no bloquea T7)**: quién asigna la secuencia e-NCF. Si el PSFE firma y numera, `ElectronicInvoiceSequences` deja de asignar números y queda como registro/auditoría local → hay que confirmarlo con el PSFE antes de T14.
  - **Huecos de producto que la revisión de "mi ERP podrá facturar electrónicamente" dejó al descubierto**: (1) solo existen E31 y E32 en `SaleDocumentType` y la DGII define 10 tipos — falta la **nota de crédito**, que es la única salida fiscal de una venta ya emitida (una venta no se edita ni se borra: se corrige con nota) → **S15/T17**; (2) `Company` tiene un solo `TaxId` y un tenant con sucursales tiene varios RNC emisores → **S16/T18**; (3) T8 "alta del tenant en el PSFE" no es un endpoint: es acuerdo comercial, cuentas, precios por comprobante y registro del `.p12` → es **cronograma, no código**.

- **L12** (2026-11-03): T7 cierra la entidad de certificado y la credencial cifrada en reposo (`6261501` núcleo + `622341c` API). Decisiones:
  - **La credencial se cifra con AES-256-GCM y clave derivada por tenant** (HKDF-SHA256 con el `companyId` como salt) a partir de una clave maestra de configuración (`Encryption:MasterKey`, base64 de 32 bytes). La clave maestra **no se puede rotar en sitio**: cambiarla deja ilegibles todas las credenciales guardadas. La app **no arranca** con una clave ausente o inválida (falla cerrada, no degrada).
  - **La credencial es un valor opaco**: bajo Option A es la API key del PSFE y bajo Option B sería el `.p12` sellado con su password. La entidad no cambia con la decisión estratégica.
  - **El material sensible no sale por la API**: el DTO de lectura no tiene dónde alojarlo, y el test serializa la respuesta y verifica que ni el texto claro ni el ciphertext aparecen.
  - **El RNC del emisor se valida con el VO `Rnc` y se normaliza a dígitos** antes de persistir, igual que `Client.TaxId` y `Company.TaxId`.
  - **Verificación**: 312/312 tests. Los 21 tests de T7 se validaron además **rompiendo la implementación a propósito** (derivación de clave por tenant anulada → cae el test de aislamiento entre tenants; cifrado reemplazado por texto claro → cae el test de credencial cifrada; ningún otro test se movió). La migración `20261103233836_AddCompanyCertificates` se aplicó y se verificó contra Postgres real, y una sonda temporal confirmó que la columna guardada **no contiene el texto claro** (la sonda se borró después: contenía el password de la base).
  - **Verificador independiente** (riesgo item 2, credenciales): 9/9 chequeos PASS, 0 blockers, 6 advisories. Dos advisories eran defectos del propio T7 y se corrigieron: el README documentaba `/api/company-certificates` (la ruta real es `/api/CompanyCertificates` → 404) y el conteo estaba inflado (307 en vez de 306) porque un `--no-build` mantuvo en el binario la sonda temporal ya borrada. Los advisories que quedan son: la clave maestra de desarrollo viaja en `appsettings.json` (igual que la clave JWT y el password de la base, que ya venían así), la clave maestra **no se puede rotar en sitio** porque la entidad no guarda un key id, y el README ya documentaba `/api/accounting-transactions`, una ruta que el API tampoco sirve (preexistente, fuera de alcance).
  - **6 tests de integración** se tomaron desde las sondas del verificador, porque el suite comiteado solo probaba el protector y el service por separado: la respuesta HTTP no lleva ni el texto claro ni el ciphertext y la fila guardada sí queda cifrada; otro tenant no puede ni leer ni listar el certificado; y un usuario que no es admin no puede crearlo.
  - **Revisión nativa del candidato: OMITIDA por el usuario** ("Omitir por esta vez" en el consentimiento de Gentle AI) → sin registro de revisión y sin autoridad consumida (`declined_this_candidate`); RDD sigue activo para los próximos cambios. La verificación técnica no la reemplaza: el verificador independiente corrió y dio 9/9 PASS con 0 blockers.
  - **Correcciones de catálogo confirmadas el 2026-11-03** contra el set de pruebas de certificación de dos proveedores independientes: **E34 = Nota de Crédito** y **E33 = Nota de Débito** (la ambigüedad de S15 queda cerrada) y **el contribuyente solicita el rango de e-NCF en su Oficina Virtual, no el proveedor** → el rol de T5 se confirma: la DGII autoriza el rango y el proveedor asigna los números dentro de él.
  - **Contexto nuevo (el usuario)**: un developer amigo suyo creó su propia API de facturación electrónica y **certifica a sus clientes él mismo, sin terceros**. Eso es exactamente la **Option B** del plan (Volterp como plataforma/proveedor que custodia el `.p12` de cada cliente y firma), y es evidencia de que ese camino lo puede recorrer un independiente, no solo un proveedor grande. Lo que sigue exigiendo la DGII es lo que dice la tabla de caminos: ser emisor electrónico, tener **≥3 contribuyentes ya certificados como emisores electrónicos**, actividad de venta y/o desarrollo de software en el RNC, estar al día con obligaciones tributarias y presentar la solicitud con anexos. Queda **pendiente de decisión del usuario** si se adelanta esa vía (Fase 4) o se mantiene Option A hasta tener clientes emisores.

- **L13** (2026-11-04, verbatim): "champ ayer comenzamos a trabajar la facturacion electronica y quiero saber si completamos todo los pasos que tenemos y que nos falta, tambien lo de usar una api de terceros no me cuadra mucho quiero tener mi propia implementación para que si pasa algo no estar atado de manos."
  - **Estado verificado contra el código** (no contra el doc): la rama `feature/electronic-invoicing` lleva **21 commits** sobre `development` y la suite está en **312/312 verdes**. La **Fase 0 está cerrada** (T1–T6) junto con T7 y T9. La **Fase 1 queda con T17 y T18 abiertas**, la **Fase 2 entera** (T10–T13) y la **Fase 3 entera** (T14–T16) **sin empezar**: hoy **ningún código de Volterp emite un comprobante**. T8 no es código, es acuerdo comercial con el PSFE. El plazo de la DGII para pequeños y micro es el **15-nov-2026** (11 días desde hoy) y ninguna fase completa entra en ese plazo.
  - **T9 estaba marcado como pendiente en la tabla pero ya estaba cerrado en código** (`714117f`): la fila queda corregida. La única fila de Fase 1 que quedaba abierta por código real era T17 y T18.
  - **Hay un cambio sin commitear ajeno a esta feature**: `Volterp.Application/Services/ClientService.cs` agrega validación de email (`IsValidEmail()`, que ya existe en `Application/Helpers`) y reemplaza la asignación directa de `UpdatedAt` por `MarkUpdated()` (de `AuditEntity`), alineando el service con el patrón que ya usan `SupplierService` y `UserService`. Compila y los 312 tests pasan con él, pero **no pertenece al alcance e-CF** y queda pendiente de decisión: commitearlo aparte o descartarlo.
  - **La objeción a Option A tiene tres capas, no una** — y cada una tiene una mitigación distinta:
    1. *Lock-in de código*: mitigado **por diseño** (S14: el transporte vive detrás de una interfaz propia y el contrato con el cliente no cambia al cambiar de proveedor), pero **la interfaz todavía no existe**: T14 está sin empezar. Es la mitigación más barata y la que ataca el "atado" en el punto exacto donde duele.
    2. *Lock-in de custodia*: bajo Option A el `.p12` vive cifrado en el PSFE y nosotros no lo tocamos. Pasar a implementación propia exige que **cada cliente entregue su certificado y consienta** — es una migración comercial, no de código (ya anotado en L11 y en la Fase 4).
    3. *Lock-in regulatorio*: el duro. Para firmar en nombre de **varios** contribuyentes hay que certificarse como **PSFE** (≥3 contribuyentes ya certificados como emisores electrónicos, actividad de desarrollo de software en el RNC, al día con tributación, solicitud con anexos).
  - **Lo que ninguna decisión cambia**: la certificación es un **trámite con plazos de la DGII**, no una dependencia de código. El trabajo técnico pesado —la firma XMLDSig (T12) y el cliente de los web services de la DGII— **se puede construir y probar antes** de la certificación (contra el set de pruebas de la DGII). Lo que la certificación bloquea es el **uso comercial para terceros**, no el desarrollo. Esa distinción es la que hace viable construir la implementación propia en paralelo en vez de esperarla.
  - **Pendiente de decisión del usuario**: qué vía se toma. La tabla de fases sigue con Option A; este requerimiento pide "mi propia implementación", lo que **reabre la decisión de la Fase 4** y, con ella, el orden de T12 y T14. Ninguna tarea de código se arranca hasta que esa decisión esté tomada.

- **L14** (2026-11-04, verbatim): "Mi colega me comento ayer que el basicamente lo que hace es hacer la comunicacion con la dgii pero que la firma y todo eso de los xml los hace con los certificados de los clientes ellos mismos tienen sus credenciales de la dgii y el solo provee la comunicacion por el momento es eso lo que necesitamos".
  - **El modelo queda confirmado y el plan se reescribe alrededor de él**: Volterp **no** se integra con la API de un PSFE. Volterp **habla con la DGII** y **firma con el certificado del cliente**; el cliente conserva sus credenciales de la DGII. Option A (eCF MSeller) deja de ser el camino de transporte y queda solo como registro de lo evaluado.
  - **Evidencia que lo respalda, y no es una suposición**: verificada contra la documentación de dos proveedores (#1821), en el **Portal de Certificación de la DGII** el contribuyente registra el software que va a usar como **"Tipo EXTERNO"**, con los datos del proveedor (RNC, razón social, nombre comercial) y **las URLs del proveedor para recepción y aprobación comercial**. Ese es exactamente el rol que describe el colega: el software es el proveedor externo, el contribuyente conserva su certificado y sus credenciales. **El modelo del colega es el que la DGII tiene previsto para un software registrado como proveedor externo.**
  - **Cambios aplicados al plan**: T14 pasa de "cliente de la API de MSeller" a **"cliente de los web services de la DGII"** (los 5 endpoints del blueprint de Nurbansoft); **T12 (firma) deja de ser opcional y pasa a ser la primera pieza**; T15 (recepción + aprobación comercial) **entra al camino crítico** porque el cliente registra **nuestras URLs** en su certificación; T8 pasa de "alta en el PSFE" a "certificación del cliente por sistema de desarrollo propio, registrándonos como Tipo EXTERNO"; y **S7 se reescribe** (ver abajo).
  - **S7 se refina**: la credencial cifrada que T7 ya guarda **es el certificado del cliente** (bajo Option A habría sido la API key del PSFE — la entidad no cambia, que era el punto de designedla como valor opaco desde el inicio). Pero el emisor tiene **dos** secretos: el **`.p12` + password** (para firmar) y la **credencial de usuario administrador de e-CF** (para pedir la semilla que autentica los web services). La entidad actual tiene **un** campo `EncryptedCredential` → **S7 queda pendiente de forma**: un campo con dos credenciales cifradas, o dos entidades.
  - **Lo que el modelo agrega y hay que absorber**: nuestra API **deja de ser solo para nuestros clientes** — expone endpoints públicos que **la DGII consume**. Es el precio del modelo y no es evitable, porque el cliente registra nuestras URLs. Eso cambia la postura de seguridad de la API y pone a T15 en el camino crítico de la certificación del cliente.
  - **Pregunta abierta con dueño**: ¿la DGII exige certificación PSFE para operar así? No se puede cerrar desde la documentación (`dgii.gov.do` inaccesible desde esta máquina) y **la persona que mejor lo puede responder es el colega**, que lo hace en producción: hay que preguntarle si él se certificó como PSFE o si sus clientes lo registran como proveedor "Tipo EXTERNO" **sin** certificación PSFE. Esa respuesta decide si la Fase 4 existe.
  - **Consecuencia del plazo, sin adornos**: con el modelo confirmado las dos piezas más pesadas (T12 y T14) son nuestras, y **ninguna de las dos está empezada**. Con 11 días para el 15-nov-2026, **Volterp no emite en producción antes del plazo**. Los clientes que no lleguen a tiempo usan el Facturador Gratuito de la DGII. El plan deja de fingir que hay un camino corto y el plazo real hay que fijarlo explícitamente.
  - **Pendiente de decisión del usuario**: el orden de arranque. Con T10-T16 sin empezar, la secuencia candidata es T17+T18 (cerrar Fase 1, barato) → T10+T11 (dominio e-CF, sin dependencia de la DGII) → **T12 (firma)** → T13 → T14 → T15. Lo que no se puede hacer es empezar por T14 sin T12: sin firma no hay nada que enviar.

- **L15** (2026-11-04, verbatim): "valida esta informacion contra lo que la dgii pide y en que posicion nos coloca ya que tenemos poco tiempo para salir al mercado, tambien debemos adapatar el front con todas las mejoras" · "si hay un front pero luego trabajamos con eso vamos a crear nuestra implementacion con el proceso de la dgii no podemos perder tiempo" · "la arquitectura la vamos a hacer como un proyecto separado un class lib aparte para que si en un futuro queremos vender el servicio a terceros lo puedan consumir" · "pero dentro de la misma solucion" · "valida tambien contra las memorias de NurbanSoft ahi hay mucha tela que cortar" · "pero ya terminamos el plan?"
  - **El plan NO está terminado**: 10 de 19 tareas de código. La Fase 0 (dominio fiscal) está cerrada, pero **la Fase 2 y la Fase 3 — lo único que emite un comprobante — están en cero**. Y el frontend no existe en este repo (es otro repositorio): la adaptación del front es trabajo abierto, no hecho.
  - **Validación contra la DGII (Guía 6 descargada y leída)**: el modelo del colega **resiste**. La Guía 6 confirma las tres vías como opciones **del emisor** ("Los emisores de e-CF deben ser previamente autorizados por DGII y utilizar un sistema que permita generar y recibir el formato de especificación... Sistema de desarrollo propio / A través de Proveedores... / Facturador Gratuito") → un software registrado como **Tipo EXTERNO** que el contribuyente certifica es exactamente la vía de **sistema de desarrollo propio**, que es la que no exige ser PSFE. **Confirma los 10 tipos de e-CF**, la estructura `E + tipo + 10 dígitos`, y los 7 requisitos de emisor electrónico.
  - **Dos correcciones que salieron de esa lectura**: (1) **S9 estaba mal** — la DGII define **cinco** veredictos (Aceptado, Rechazado, **Aceptado condicional**, **En proceso**, **Anulado**), no dos; el "aceptado condicional" es el que más traga en un ERP porque hay que corregir la causa raíz; (2) **hueco de producto nuevo (S17/T19)**: la **Representación Impresa (RI)** es obligatoria para el receptor **no electrónico**, con layout propio (datos del emisor y del cliente, tabla de líneas, Subtotal Gravado, Total ITBIS, Total, código de seguridad, fecha de firma digital y QR) → **toda venta de consumo necesita una RI imprimible y hoy no hay nada que la genere**.
  - **Validación contra las memorias de Nurbansoft** (proyecto `recnah`, #144–#152 — "mucha tela" confirmada): la capa **INBOUND ya estaba hecha y funcionando**: los 4 endpoints (`GET /fe/autenticacion/api/semilla` · `POST /fe/autenticacion/api/validacioncertificado` · `POST /fe/recepcion/api/ecf` · `POST /fe/aprobacioncomercial/api/ecf`), 3 tablas (`AuthenticationSeed`, `EcfReceived`, `EcfCommercialApproval`) y la lógica de **validación de firma, certificado y token**. La semilla devuelve **XML, no JSON** (`<Semilla><Valor/><Fecha/></Semilla>`) — un bug que ahí costó un ciclo. Dos trampas de EF: Core pluraliza el `DbSet` (`AuthenticationSeeds` en vez de `AuthenticationSeed`) y la columna de RNC tuvo que crecer de `NVARCHAR(20)` a `NVARCHAR(500)` por valores encriptados. La **firma** ("Firma XML DGII", #148) quedó **diseñada y nunca implementada**: es exactamente T12, y su diseño ya era el correcto (certificado automático por `CustomerId`, nunca exponer password/ruta/llave privada, preservar el XML original, **validar la firma generada**, historial y auditoría).
  - **T12 cerrado** (`XmlDsigEcfSigner` en la library nueva): firma enveloped sobre el documento completo, RSA-SHA256, digest SHA-256, C14N, `KeyInfo` con el certificado del emisor para que el receptor pueda validar sin conocerlo de antemano, y **fail-closed**: la firma se verifica antes de devolverse, así que nunca sale una firma inválida. **10/10 tests nuevos + 312/312 los preexistentes**. **Evidencia de que los tests fijan reglas y no alazar**: (a) cambiar el método de firma a RSA-SHA1 → cae **exactamente 1** test (`Sign_UsesTheProfileTheDgiiRequires`) y los otros 9 quedan verdes; (b) quitar el transform enveloped → caen 8 en cascada, y la cascada **es la prueba del guard**: el firmador lanza y no devuelve nada, en vez de devolver una firma que no verifica.
  - **Arquitectura**: `Volterp.ElectronicInvoicing` + `Volterp.ElectronicInvoicing.Tests`, dentro de la misma solución, **sin referenciar ningún otro proyecto de Volterp** → consumible por terceros. Los catálogos de la DGII (`TaxCode`, `EcfDocumentType`) se mudaron a la library y `SaleDocumentType` se eliminó: **un solo enum de tipos, con los 10 oficiales** → **T17 (nota de crédito E34) queda resuelto de regalo**, porque el enum ya lo trae. Los 312 tests preexistentes siguen verdes después del movimiento.
  - **Pendiente de confirmación con el colega** (no bloquea el código): si la DGII exige certificación PSFE para operar este modelo. La library hace el mismo trabajo técnico en los dos escenarios, así que la respuesta cambia el trámite, no el código.

- **L16** (2026-11-04, verbatim): "aplica tambien la ultima migracion que creamos para electronic invoicing" · "porque electronic invoice y el sale son 2 entidades separadas" · "porque no podemos tener entidades que hagan lo mismo" · "que estas removiendo?" · "veo que sigues implementando tests?" · "Ok continua" · "Deja los gaps de purchase como un detalle de implementacion para mas luego por el momento continuemos nuestro desarrollo".
  - **T10 cerrada** (`cd41f9c`): entidad `ElectronicInvoice` + máquina de estados con los 5 veredictos de la DGII, 22 tests, **334/334 + 10/10 verdes**. Migración `20261104163914_AddElectronicInvoices` **aplicada y verificada contra Postgres real**: índice único `(IssuerTaxId, ENCF)`, FKs `ON DELETE RESTRICT`, `DocumentType` y `Status` **sin default 0** (la trampa de T4 no aplica en tablas nuevas) y `DocumentType` con inicializador `= EcfDocumentType.Consumer` en la entidad.
  - **El usuario exigió explicación del alcance** ("porque estas tocando codigo que no te pedi que tocaras"): `CompanyConfiguration.cs` **no se tocó** (cero cambios) — lo que se tocó fue `Company.cs`, `Sale.cs` y `VolterpDbContext.cs`, **una línea aditiva cada uno**. Lo removido en `28ee400` fueron los 2 enums de `Volterp.Domain/Enums`, movidos a la library para no duplicar el catálogo de la DGII. Regla que sale de ahí: **anunciar los comandos que saltan verificaciones de seguridad antes de ejecutarlos** (`dotnet ef migrations remove --force`).
  - **Por qué `ElectronicInvoice` y `Sale` son dos entidades** (respondido y aceptado por el usuario): la relación es **1-a-N** (una venta genera el e-CF original y después las notas de crédito E34 y débito E33 que lo corrigen; una venta emitida no se edita ni se borra), el **ciclo de vida es independiente** (la venta queda `Completed` para siempre; el e-CF puede terminar en Rechazado o Anulado sin que la venta cambie, y un reenvío tras un rechazo no puede pisar el intento anterior) y **la firma es sobre el XML fiscal**, no sobre la venta. Además, si el e-CF viviera dentro de `Sale`, la library arrastraría la forma del ERP y **dejaría de ser vendible por separado** — que es el requirement explícito del usuario.
  - **Dos huecos de diseño que la pregunta destapó** (el usuario los dejó como detalle de implementación posterior): (1) `ElectronicInvoice.SaleId` es no-null → **no se puede representar un e-CF recibido** (E41, comprobante de compra); (2) `Purchase` no tiene desglose fiscal (`Subtotal`/`Itbis`/`TaxCode` por línea), así que **no se puede llenar la declaración de ITBIS**.
  - **Los documentos oficiales de la DGII sí se Bajan** — lo que fallaba antes era la ruta, no el sitio. Con 6 de 7 descargados y leídos: **T12 queda validada contra la spec oficial** (coincide línea por línea con el C# de referencia de la DGII) y **T11 y T14 quedan desbloqueadas**. El mapa de campos del formato oficial quedó commiteado en `docs/ecf-format-v1.0.md`.
  - **Lo que NO se cierra**: la extracción automática del PDF no alinea de forma confiable los tipos, largos y la matriz de obligatoriedad por tipo (el documento usa tablas anidadas que rompen la extracción). Los **tags y su orden** sí son confiables y están commiteados; los tipos/largos hay que leerlos del PDF **a mano** antes de escribir el validador. No se genera un validador fiscal sobre datos dudosos.

- **L17** (2026-11-04, verbatim): "recuerdo que en la implementacion que hice de NurbanSoft nosotros construiamos el formato xml dinamicamente en base al tipo de comprobante" · "vamos con la fase que es correcta de una ves, nosotros podemos tener el xml formateado en la app no veo problema con eso" · "y dependiendo del tipo de comprobante usarlo" · "en que fase estamos y que estamos tratando de hacer ahora mantenme informado".
  - **La memoria de Nurbansoft era la guía correcta**: el XML se construye **dinámicamente según el tipo de comprobante**. Los XSD oficiales lo confirman: hay **un esquema por tipo** y lo que cambia entre ellos es qué campos existen, cuáles son obligatorios y en qué orden. Por eso el motor es **uno solo** y las diferencias por tipo son **datos** (el esquema), no diez variantes del código.
  - **El XSD resuelve lo que el PDF no decía**: la raíz del documento es **`<ECF>`** (no `e-CF`), las 8 secciones del PDF son exactamente `Encabezado` (`IdDoc`/`Emisor`/`Comprador`/`Totales`), `DetallesItems`, `Subtotales`, `DescuentosORecargos`, `Paginacion`, `InformacionReferencia` y `FechaHoraFirma`. Formatos: `<Version>` = `1.0`, fechas **`dd-MM-AAAA`**, `FechaHoraFirma` = `dd-MM-AAAA HH:mm:ss` (GMT-4), e-NCF de 13 alfanuméricos, RNC de 9 u 11 dígitos, teléfono `ddd-ddd-dddd`, dinero con 16 enteros y 2 decimales, precio unitario hasta 4 decimales, `Item` 1..1000, `FormaDePago` 1..7, `Subtotal`/`DescuentoORecargo`/`ImpuestoAdicional` 1..20, `Pagina` 1..1000.
  - **La lista de tipos cambió en la reforma 2025 y yo la tenía invertida**: **31 = Factura de Crédito Fiscal**, **32 = Factura de Consumo**, 33 = Nota de Débito, 34 = Nota de Crédito, 41 = Compras, 43 = Gastos Menores, 44 = Regímenes Especiales, 45 = Gubernamental, 46 = Exportaciones, 47 = Pagos al Exterior. El enum `EcfDocumentType` de la library **ya estaba bien** (los comentarios del propio XSD lo confirman); el error era de mi lectura del PDF extraído.
  - **El usuario decidió ir con la fase correcta**: los XSD oficiales se **copian al repo** (`Volterp.ElectronicInvoicing/Schemas/ecf-NN.xsd`) y se **embeben** en la library, para que pueda **escribir el XML y validarlo contra el esquema de la DGII antes de firmarlo y enviarlo**.
  - **El XML que viaja va sin formato, el que se muestra en la app puede ir formateado**: el código de referencia de la DGII hace `parser.setPreserveWhitespace(false)` — **ellos descartan el whitespace antes de digerir**. Si el documentoteenth llega con whitespace y ellos lo descartan, el digest no coincide. Por eso (a) el writer emite el documento **sin indentación**, (b) `XmlDsigEcfSigner` pasó a `PreserveWhitespace = false` **igual que la DGII**, y (c) hay un test que prueba que **un documento que llega formateado produce el mismo digest** que uno que no llega formateado. La app puede mostrarlo indentado: es una cuestión de vista, no de transmisión.
  - **Lo que el XSD reveló sobre el firmador**: el esquema termina en `<xs:any minOccurs="1"/>`, o sea **la ranura de la firma es obligatoria** → un documento sin firmar es inválido por definición. La validación tiene dos modos: `Validate` (exige la firma, para el documento firmado) y `ValidateUnsigned` (para el documento recién escrito, con la ranura sustituida por un marcador).
  - **Bug real encontrado y corregido**: el writer escribía sobre un `StringBuilder`, así que la declaración XML salía como `encoding="utf-16"` mientras los bytes viajan en UTF-8 → la DGII lo rechazaría. Ahora escribe sobre un `MemoryStream` con UTF-8 sin BOM.
  - **Pendiente de T11**: el mapeo `Sale` → modelo neutral en la capa Application y el cálculo de los totales según las tasas de ITBIS (el `IndicadorFacturacion` del ítem ya las codifica: 1 = tasa 1, 2 = tasa 2, 3 = tasa 3, 4 = exento).
  - **Assessment de RDD sobre los commits nuevos** (`aad71fb`, `7da7f2e`, `3235fe7`): `risk: medium`, `review_due: false` (`under_budget`), 147 paths / 29.489 líneas acumuladas desde `d08a830`. **No hay revisión que correr todavía**: la frontera de revisión se fija por porción al abrir el pull request, no sobre la rama acumulada.
  - **L18 — T11b cerrado** (`49e159a`, `2c0beb3`, `da65e34`, `71f808d`). El gap que quedaba era el mapeo `Sale` → modelo neutral y los totales por tasa. **Decisión del usuario: no crear enums nuevos** — `EcfTypeIncome`, `EcfPaymentType` y `EcfItemKind` ya existían en `EcfCatalogs.cs`; lo que faltaba era un **helper por tipo de comprobante**, y ese es `EcfTypeProfiles` (deriva del `minOccurs` del XSD oficial: `FechaVencimientoSecuencia` es obligatoria en 31/33/41-47 y **no existe** en 32/34; `<Comprador>` es obligatoria en 31 **y** 32).
  - **L18 — el `MontoItem` es la base SIN impuesto**: es lo que suma en `MontoGravadoTotal`; `MontoTotal` = gravado + exento + ITBIS, y lo no facturable queda fuera del total. Confirmado contra `ecf-fields.json`.
  - **L18 — bug real del writer encontrado por el test nuevo**: `EcfXmlWriter.WritePhoneNumbers` abría `<TelefonoEmisor>` y `WriteElement` lo escribía otra vez → XML inválido en cualquier empresa con teléfono. Corregido y con test (`Write_EmitsThePhoneNumbersOfTheIssuer`).
  - **L18 — la revisión nativa quedó parcial**: assessment `risk: high` (escalado con `--escalate-item 3`, 12 paths / 793 líneas), lineage `review-ba862585c11a8920`, 3 lentes. `review-risk` entró (`admission_decision: completed`); `review-readability` y `review-reliability` devolvieron **salida vacía** dos veces cada uno — el subagente del provider `minimax-coding-plan/MiniMax-M3` transmitió 4-5 minutos y no entregó JSON (el primer intento de riesgo volvió truncado a 2810 bytes y el transporte lo rechazó). **No es un defecto del código**: el mismo subagente funciona para el lens de riesgo y falla con el mismo payload en los otros dos.
  - **L18 — el único hallazgo de la revisión sí se corrigió**: el mapper no verificaba que `sale.CompanyId` fuera el emisor, o sea que el documento fiscal podía llevar el RNC, la dirección y el teléfono de **otra** empresa (hueco de tenant). Guard fail-closed + test (`da65e34`, `71f808d`).
  - **L18 — pendientes que NO son de T11b** (para el próximo work unit): la fuente de `FechaVencimientoSecuencia` (el rango de secuencia que otorga la DGII), registrar la library en el DI del ERP, la implementación ERP de `ISigningCertificateProvider`, `ElectronicInvoiceSequenceRepository.NextSequenceAsync` todavía sin llamar desde Application, `SaleRepository.GetSaleByIdAsync` sin cargar `Company`, y `Sale` sin número de línea (el mapper usa el orden de la colección).

## Relevant Files

- `Volterp.Domain/Entities/{Client,Company,Product,Sale}.cs` — modelo fiscal a construir (Fase 0)
- `Volterp.Domain/ValueObjects/Rnc.cs` — VO con el dígito verificador real de la DGII
- `Volterp.Domain/Enums/TaxCode.cs` — código de impuesto DGII por ítem (`IndicadorFacturacion` 0..4)
- `Volterp.Domain/Entities/ElectronicInvoiceSequence.cs` — secuencia de e-NCF por emisor y tipo, y el formato del número (E + 2 + 10)
- `Volterp.Infrastructure/Repositories/ElectronicInvoiceSequenceRepository.cs` — asignación atómica del siguiente número
- `Volterp.Domain/Enums/SaleDocumentType.cs` — tipo de comprobante de la venta (E31 crédito fiscal / E32 consumo)
- `Volterp.Domain/Abstractions/ITenantScoped.cs` — declaración de tenant scope de las entidades nuevas
- `Volterp.Domain/Entities/CompanyCertificate.cs` — certificado del tenant: metadata pública + credencial cifrada
- `Volterp.Infrastructure/Services/AesCredentialProtector.cs` — cifrado en reposo (AES-256-GCM, clave derivada por tenant)
- `Volterp.Application/Services/CompanyCertificateService.cs` — alta y lectura; cifra antes de persistir y nunca devuelve la credencial
- `Volterp.Api/Controllers/CompanyCertificatesController.cs` — endpoints del certificado; la respuesta no lleva material sensible
- `Volterp.Infrastructure/Data/TenantScopeGuard.cs` — falla el arranque si una entidad con `CompanyId` no declara `ITenantScoped`
- `Volterp.Application/Services/SaleService.cs` — `CompleteSaleAsync` es hoy el punto donde el e-CF se emitiría; el desglose fiscal se calcula en `Sale.CalculateTotals`
- `Volterp.Application/Services/ClientService.cs` — alta y persistencia del RNC del receptor
- `odd/tasks/multi-tenant-isolation.md` — precedente de tracking doc de ODD en este repo

### T11b — el mapeo `Sale` → e-CF

- `Volterp.Application/Mappers/SaleEcfDocumentMapper.cs` — `Map(Sale, Company, EcfIssuance)`: fail-closed, y exige que la venta pertenezca a la empresa que emite
- `Volterp.ElectronicInvoicing/EcfTaxRates.cs` — tabla única de tasas por `TaxCode` (bucket, fracción y %)
- `Volterp.ElectronicInvoicing/Xml/EcfTotalsCalculator.cs` — totales del área "Totales" por tasa de ITBIS
- `Volterp.ElectronicInvoicing/Xml/EcfTypeProfiles.cs` — helper por tipo de comprobante derivado del `minOccurs` del XSD
- `Volterp.ElectronicInvoicing/Xml/EcfDocument.cs` — `EcfIdentity.SequenceExpiresAt` ahora `DateTime?`
- `Volterp.ElectronicInvoicing/Xml/EcfItem.cs` — `Amount` documentado como "before taxes"
- `Volterp.ElectronicInvoicing/Xml/EcfXmlWriter.cs` — corregido el anidamiento de `<TelefonoEmisor>`
- `Volterp.ElectronicInvoicing.Tests/Xml/EcfTotalsCalculatorTests.cs` y `EcfTypeProfilesTests.cs` — tests de la library
- `Volterp.ElectronicInvoicing.Tests/Xml/EcfXmlWriterTests.cs` — fixture `MontoItem` corregido + test del teléfono
- `Volterp.Tests/Mappers/SaleEcfDocumentMapperTests.cs` — tests del mapper + round-trip contra el XSD oficial (31 y 32)
- `Volterp.Domain/Entities/Sale.cs` — `TaxRate` delega en `EcfTaxRates.FractionOf`

## La library portable

- `Volterp.ElectronicInvoicing/Volterp.ElectronicInvoicing.csproj` — la class library vendible: **sin referenciar ningún otro proyecto de Volterp**
- `Volterp.ElectronicInvoicing/EcfDocumentType.cs` — los 10 tipos de e-CF de la DGII (sustituye a `SaleDocumentType`)
- `Volterp.ElectronicInvoicing/TaxCode.cs` — el `IndicadorFacturacion` de la DGII (movido desde `Volterp.Domain.Enums`)
- `Volterp.ElectronicInvoicing/Abstractions/IEcfSigner.cs` — el contrato de firma
- `Volterp.ElectronicInvoicing/Abstractions/ISigningCertificateProvider.cs` — el puerto del `.p12` del emisor, indexado por RNC emisor
- `Volterp.ElectronicInvoicing/Signing/XmlDsigEcfSigner.cs` — **T12**: firma enveloped RSA-SHA256, digest SHA-256, fail-closed
- `Volterp.ElectronicInvoicing.Tests/Signing/XmlDsigEcfSignerTests.cs` — 10 tests del firmador
- `docs/ecf-format-v1.0.md` — **el contrato del formato oficial del e-CF**: los 217 tags del XML agrupados por las 8 secciones de la DGII, extraídos del PDF oficial. Es la entrada de T11.
- `Volterp.ElectronicInvoicing/Schemas/ecf-NN.xsd` — **los esquemas oficiales de la DGII, embebidos en la library**: la raíz, el orden, los campos por tipo y su obligatoriedad. La library escribe y valida contra ellos.
- `Volterp.ElectronicInvoicing/Xml/{EcfDocument,EcfItem,EcfCatalogs,EcfXmlWriter,EcfXmlException}.cs` — el modelo neutral del e-CF, los catálogos de la DGII y el writer dinámico por tipo.
- `Volterp.ElectronicInvoicing/Schemas/EcfSchema.cs` — la introspección del XSD: qué admite y qué exige cada tipo de comprobante, y la validación contra el esquema oficial.

## Reference documents (oficiales, para T12 / T14 / T15)

- ✅ **"Guía del Contribuyente No. 6 — Facturación Electrónica"** — **descargada y leída** (2,5 MB, `dgii.gov.do/publicacionesOficiales/bibliotecaVirtual/contribuyentes/facturacion/Documents/Facturación%20Electrónica/6%20Guia%20Facturacion%20Electronica.pdf`). Es la que valida el modelo, los 10 tipos, los 7 requisitos de emisor electrónico, los 5 estados del e-CF, la RI, el QR, el código de seguridad y los plazos.
- ✅ **"Formato Comprobante Fiscal Electrónico (e-CF) V 1.0"** — DGII, modificado 30/10/2025, 2,2MB, 89 páginas → **descargada y leída**. La estructura XML del comprobante, el `IndicadorFacturacion` (base de S3) y el `Encabezado` con los 13 caracteres del e-NCF. **T11 queda desbloqueada.** El mapa de campos extraído y commiteado en `docs/ecf-format-v1.0.md` (217 tags, agrupados por las 8 secciones oficiales).
- ✅ **"Firmado de e-CF"** — DGII, modificado 22/11/2023, 1,2MB, 20 páginas → **descargada y leída**. **T12 queda validada contra la spec oficial**: el C# de referencia de la DGII coincide línea por línea con `XmlDsigEcfSigner` (C14N **inclusiva**, `xmldsig-more#rsa-sha256`, `Reference URI=""`, transform enveloped, digest `xmlenc#sha256`, `KeyInfo/X509Data`). La spec dice que SHA256 es **obligatorio**.
- ✅ **"Descripción Técnica Servicios Emisores Electrónicos"** — DGII, modificado 29/05/2026, 1,6MB, 13 páginas → **descargada**. El contrato de los web services (semilla, envío, recepción, aprobación comercial). **Es el documento que T14 implementa.**
- ✅ **"Descripción Técnica Servicios DGII"** — DGII, modificado 29/05/2026, 2,3MB, 47 páginas → **descargada**. El otro lado del contrato: lo que la DGII espera de nosotros.
- ✅ **"Formato Aprobación Comercial v1.0"** — DGII → **descargada**. Parte de T15.
- ✅ **"Formato Anulación de e-NCF v1.0"** — DGII → **descargada**. La anulación de rangos reservados (el hueco que dejó T5).
- ⬜ **"Proceso de Certificación para ser Emisor Electrónico"** — DGII, modificado 19/08/2025, 1,6MB → el paso a paso de la certificación del cliente (la vía "Tipo EXTERNO" de T8). La ruta con el acento corregido devuelve 404: el archivo vive en `Documentacin sobre eCF` y su nombre lleva otro encoding.
- ⬜ **"Instructivo de Contingencia de Facturación Electrónica"** — DGII, modificado 15/10/2026 → qué hacer cuando la DGII no está disponible (parte de T13).
- ⬜ **"Formato Acuse de Recibo v1.0"** — el acuse de recibo del envío; 404 en la ruta probada.
- ✅ **XSD oficiales (Documentación Técnica)** — la sección "Documentación Técnica (XSD)" de la página `documentacionSobreE-CF.aspx` publica **un esquema por tipo**: `e-CF 31/32/33/34/41/43/44/45/46/47 v1.0.xsd` (14 de 15 descargados; `ACECF v1.0.xsd` da 404). Ruta: `.../Documentacin%20sobre%20eCF/Documentaci%C3%B3n%20T%C3%A9cnica%20(XSD)/<archivo>` — la subcarpeta **sí** lleva acento, la carpeta padre no. **Son la fuente de verdad del formato**: dicen la raíz (`<ECF>`), el orden de las áreas, qué campos existen en cada tipo, cuáles son obligatorios (`minOccurs`) y con qué formato (patrones de fecha `dd-MM-AAAA`, `HH:mm:ss`, RNC de 9 u 11 dígitos, e-NCF de 13 alfanuméricos, dinero con 2 decimales). Copiados al repo como `Volterp.ElectronicInvoicing/Schemas/ecf-NN.xsd` y embebidos en la library.
- **Ojo (corrige lo que decía antes)**: los PDF **sí se descargan**, pero **no** por la ruta de la biblioteca SharePoint: la ruta que funciona es `https://dgii.gov.do/cicloContribuyente/facturacion/comprobantesFiscalesElectronicosE-CF/Documentacin%20sobre%20eCF/...` — **la carpeta va sin acento** (`Documentacin`, no `Documentaci%C3%B3n`). Codificar el acento da 404.
