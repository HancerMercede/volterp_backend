# Manuales en PDF: para el que programa y para el que usa

Feature: `manuales-pdf`
Estado: **PDF rehechos y verificados. Falta la revisión del dueño (MP5) y las capturas reales (MP4b).**
Delivery: ask-on-risk
Pipeline: **HTML + CSS → PDF** (nunca librerías nativas de PDF: el diseño se hace en HTML y se convierte — la guía del pipeline es la skill `styled-pdf`)

Objetivo: tres PDF que sirvan de guía para el que se suma al equipo y para el que usa el sistema, con detalle absoluto.

## Specs

### S1

> "mira al final vamos a crear varios pdf: primero 2 para el programador como construimos el frontend que modulos como esta el codigo, detalle absoluto esto servira de guia para los nuevos dev que se sumen al proyecto de como se trabaja, tambien el backend tendra el suyo con la misma esencia, y uno para el usuario del sistema detalle absoluto de cada cosa de como se hacen cada cosa."

### S2

*Derivada de S1*: los tres entregables son:

| # | PDF | Para quién | Qué tiene |
|---|---|---|---|
| 1 | **Guía del frontend** | el programador | cómo se construye el front: qué módulos hay, cómo está el código, los patrones, las convenciones, el flujo de trabajo y **cómo trabajamos** |
| 2 | **Guía del backend** | el programador | lo mismo, con la misma esencia |
| 3 | **Manual del usuario** | el que usa el sistema | cada cosa del sistema y **cómo se hace cada cosa**, paso a paso |

### S3

*Derivada de S1 y verificada*: **las fuentes de los dos PDF de programador ya existen** y son exactamente "detalle absoluto de cómo se construye":

- **Front**: `erp-mvp/FRONTEND-SKELETON-TEMPLATE.md` (4299 líneas) — PARTE 1 SKELETON (estructura de carpetas, capas, config, i18n, contrato visual) · PARTE 2 TEMPLATE (el código de cada capa: infrastructure, stores, hooks, UI, utils, i18n, auth, routing) · PARTE 3 WORKFLOW (cómo se crea un proyecto desde el template, cómo se agrega una entidad) · PARTE 4 CONVENCIONES (naming, error handling, paginación, i18n, forms, tablas, estilo, decisiones intencionales, anti-patrones, niveles PRINCIPIO vs STACK).
- **Back**: `Volterp/NET-API-SKELETON-TEMPLATE.md` — el mismo esquema para la API .NET (el template del **front** es el otro; no se mezclan).

El trabajo de esos dos PDF es **renderizar los templates y completar lo que falte**, no escribir de cero.

### S4

*Derivada de S1 y verificada*: **el manual del usuario es el que hay que escribir**. Hoy el front tiene `erp-mvp/docs/QUE-OFRECEMOS.md` y `erp-mvp/docs/SISTEMA-INVENTARIO.md` (dos resúmenes de qué ofrece el sistema) → no hay un manual de uso. Las fuentes posibles: las pantallas reales (las páginas del front) y los flujos que ya están documentados en los planes ODD (`odd/tasks/*.md`).

### S5

*Derivada de S1*: el PDF de programador no es solo referencia técnica: es **la guía de cómo trabajamos** (los tags `[PRINCIPIO]` vs `[STACK]` del template ya lo expresan → en el PDF hay que explicar esa diferencia, porque es lo que un dev nuevo no puede deducir solo).

## Tasks

| ID | S# linked | Route | Status | Commit |
|---|---|---|---|---|
| MP1 | S1, S2 | inline (parent) | DONE | El **pipeline**: MD → HTML → PDF + la maqueta (portada, índice navegable, numeración, código con resaltado, bloques de "principio" y "stack", las páginas del PDF) → la guía es la skill `styled-pdf`. Commits `82c6daf`, `4aa5b61`, `8635447`. **El pipeline funciona** (markdown → HTML con la maqueta → PDF con Playwright conduciendo el Chrome del sistema) |
| MP2 | S1, S3 | inline (parent) | DONE | El **PDF del frontend** desde `FRONTEND-SKELETON-TEMPLATE.md` → **120 páginas A4** ✓ · el índice con el **número de página** de cada sección ✓ · encabezado corrido y pie por hoja ✓ · sin solapamientos ✓ · sin nada pegado al borde del papel ✓. Falta la revisión del dueño |
| MP3 | S1, S3 | inline (parent) | DONE | El **PDF del backend** desde `NET-API-SKELETON-TEMPLATE.md` → **74 páginas A4** ✓ (mismas verificaciones que MP2 ✓). El template **no se tocó** ✓ |
| MP4 | S1, S4 | inline (parent) | PARTIAL | El **manual del usuario**: el texto ya está escrito en `erp-mvp/docs/manual-usuario.md` (25 pantallas ✓, los flujos ✓, los nombres exactos de los botones ✓ y las limitaciones ✓) → el **PDF sale** ✓ (20 páginas A4 ✓) pero las **capturas de las pantallas reales** siguen sin estar (MP4b ✓) → los huecos quedan marcados como "captura pendiente" ✓ |
| MP4b | S1, S4 | inline (parent) | PENDING | Las **capturas de las pantallas reales** (25 ✓): el script `scripts/pdf/capturas.mjs` ✓ (loguea con un usuario de prueba ✓ y recorre las pantallas ✓) → falta que el sistema esté arriba (el **Postgres no está corriendo** ✓ y su carpeta `bin` no está ✓ → hay que arrancarlo ✓) |
| MP5 | S2, S5 | inline (parent) | PENDING | La **revisión**: que un dev nuevo arranque con el PDF del front sin preguntar nada, y que un usuario pueda hacer su tarea sin ayuda |

## Riesgos

- **item 6 (sin test)**: un PDF no se testea → el control es la revisión humana (MP5) + que el PDF sea navegable (índice con links, secciones con anclas) y que el código se lea (fuente monoespaciada, con resaltado y sin partir líneas).
- **El contenido envejece**: el PDF es una foto de una fecha → la portada lleva la fecha y el commit del repo del que sale, y la fuente sigue viva (los templates) → el PDF se regenera, no se edita a mano.
- **La licencia y el tono**: los PDF son del proyecto → documentar los módulos del ERP (ventas, compras, e-CF, RRHH) con los mismos nombres que usa el código y la DGII (E32, B02).

## Log

### L1 — 2026-11-21

> "mira al final vamos a crear varios pdf: primero 2 para el programador como construimos el frontend que modulos como esta el codigo, detalle absoluto esto servira de guia para los nuevos dev que se sumen al proyecto de como se trabaja, tambien el backend tendra el suyo con la misma esencia, y uno para el usuario del sistema detalle absoluto de cada cosa de como se hacen cada cosa."

**Lo que se Armando**: tres PDF al final ✓ → dos de programador (front y back ✓) y uno de usuario ✓ → el de front y el de back **se renderizan desde los templates que ya existen** ✓ (esa es la parte que sale gratis ✓) → el **manual del usuario es el que se escribe** ✓.

### L2 — 2026-11-21 — los templates son la fuente única y van par a par

> (el dueño: *"exacto la fuente unica de verdad son los templates ya que van agregando lo nuevo o mejorando lo que esta"* y *"van par a par con del desarrollo"*)

**Regla** ✓: el template se actualiza **con el desarrollo**, no al final ✓ → un patrón nuevo que aterrice en el código entra al template en la misma tanda ✓ → el PDF se **renderiza** desde el template ✓ (si el template está viejo, el PDF miente ✓).

**El hueco que ya se ve** ✓: el template del **back** (`NET-API-SKELETON-TEMPLATE.md` ✓) **no documenta el mapa de permisos** ✓ (el `PermissionRoutes` ✓ —un permiso por controller+verb ✓— ni el test de inventario que avisa de las rutas sin declarar ✓) → el patrón nació con la feature de roles-permissions ✓ y quedó afuera ✓ → entra con la actualización ✓ (tarea T8 ✓ en `odd/tasks/dotnet-api-skeleton-template.md` ✓).

### L3 — 2026-11-23 — el pipeline de los PDF y las dos guías de programador

**Lo que se armó** ✓: el pipeline (`scripts/pdf/md-to-pdf.mjs` + `scripts/pdf/maqueta.css` ✓) y las dos guías de programador (`erp-mvp/docs/guia-frontend.pdf` ✓ 85 páginas · `docs/guia-backend.pdf` ✓) → las dos se **renderizan desde el template** ✓ (que es la fuente única ✓) y el README de cada repo las enlaza ✓.

**El conversor** ✓: la skill `styled-pdf` prefiere Playwright ✓ → en esta máquina está (en el cache de npx ✓) pero su navegador no (pide el chromium 1228 y hay 1223 y 1248 ✓) → **Playwright conduce el Chrome que ya está instalado** ✓ (sin descargar nada ✓) y el conversor propio por CDP queda de respaldo ✓ → el pipeline no depende de nada instalado ✓.

**Lo que la maqueta aprendio** ✓: el `@import` de las fuentes **va primero** (después de otra regla el navegador lo ignora ✓ y el título salía con Segoe UI ✓) · el código **no usa JetBrains Mono** (ni la de la web ni la del IDE: no traen los caracteres de dibujo de los árboles ✓ y el navegador cae a otra fuente → **el árbol pierde la alineación** ✓ → va con Cascadia Mono/Consolas ✓) · el resaltado **solo cuando el bloque declara su lenguaje** (un árbol de carpetas no es C# ✓ y pintarlo de colores lo volvía ilegible ✓) · la portada **llena la hoja** (`height: 297mm` ✓ y sin el borde del `h1` ✓).

**El template del back se completó** ✓: la sección **2.6** con el módulo de e-CF (la library vendible ✓, los enums fiscales ✓, el modelo ✓, los perfiles por tipo ✓, el writer ✓, el firmador ✓, el transporte ✓, las URLs de verificación ✓, el RFCE ✓, el camino normal ✓, el DI ✓, el readiness ✓ y el ciclo de vida ✓) + la library en la regla de dependencias ✓ y en la estructura ✓ → y se **corrigió una afirmación que mentía**: el template decía *"Multi-tenant filter en service, no global query filter"* y el código real **sí** tiene un global query filter (`VolterpDbContext.ApplyTenantFilters` ✓) → una guía que enseña lo contrario del código es peor que no tener guía ✓.

**Lo que falta** ✗: el **manual del usuario** (MP4 ✓) — el inventario de pantallas ✗ → el paso a paso ✗ → las capturas de las pantallas reales ✗ (el front tiene que estar corriendo ✗).

### L5 — 2026-11-23 — los PDF no salieron bien: el dueño los manda a rehacer

> (el dueño: *"la verdad esos pdf estan mal, mandare a otro agente a hacerlo"*)

**El veredicto** ✓: los tres PDF **no sirven** ✗ → el dueño los manda a rehacer con otro agente ✓.

**Por qué no salieron bien** ✓ (la lección, para el que lo rehaga): el pipeline **convierte** el markdown ✗ → y un manual de la casa está **compuesto a mano** ✓ (cada hoja de `multi-tenant-refactor.html` es un diseño ✓: el chip ✓, el título con degradado ✓, las tarjetas de métricas ✓, la barra de la hoja ✓, el recuadro del resumen ✓, la comparativa en dos columnas ✓, la tabla de métricas ✓) → **convertir** da un documento con la tipografía y los colores de la casa ✗ pero **sin la composición de una hoja diseñada** ✗ → eso fue lo que no gustó ✓.

**Lo que sí sirve y queda** ✓:
- El **texto del manual del usuario** (`erp-mvp/docs/manual-usuario.md` ✓): las 25 pantallas ✓, los flujos con los nombres exactos de los botones ✓ y las limitaciones ✓.
- La **sección 2.6 del template del back** (el módulo e-CF y el readiness ✓) → es el contenido que faltaba ✗ ya está escrito ✓.
- El **pipeline** (`scripts/pdf/md-to-pdf.mjs` ✓): markdown → HTML → PDF sin dependencias ✓ (Playwright si está ✓, y si no el Chrome por CDP ✓) → sirve para **revisar** el resultado ✓, no para reemplazar el diseño ✓.
- La **maqueta** (`scripts/pdf/maqueta.css` ✓): los valores de la casa ✓ (la paleta ✓, la tipografía ✓, las tablas ✓, los componentes ✓) → es la referencia de los estilos ✓.
- El **modelo a copiar**: `Volterp/multi-tenant-refactor.html` ✓ (y su PDF ✓) → es el HTML hecho a mano de un manual de la casa ✓ → **ahí está el diseño que hay que lograr** ✓.

**Las tres cosas que la maqueta enseñó** ✓ (y que están en el código ✓): el `@import` de las fuentes va **primero** ✓ (después de otra regla el navegador lo ignora ✓) · el código **no** puede usar JetBrains Mono (no trae los caracteres de dibujo de los árboles ✓ → el árbol pierde la alineación ✓) → va con Cascadia Mono/Consolas ✓ · con `@page { margin: 0 }` el pie y el encabezado de Chrome se dibujan **encima** del texto ✓ → el encabezado de cada sección tiene que ir **dentro del flujo** ✓ (una barra por sección ✓).

**Lo que falta para las capturas** ✗: el **Postgres del dueño no está corriendo** ✓ y su carpeta `C:\Program Files\PostgreSQL\14\` solo tiene `data` (sin `bin` ✓) → no se puede arrancar desde el harness ✓ → el script `scripts/pdf/capturas.mjs` ✓ (loguea con `demo.admin1` / `Demo123!` ✓) está listo ✓ para cuando levante la base ✓.

### L4 — 2026-11-23 — la maqueta del PDF es la de la casa

> (el dueño: *"la guia-backend no lo veo bien, no me gusta mucho, como que no tiene el estilo de los manuales que yo hago: valida y dale el estilo y mejora el encuadre de las paginas"*)

**El estilo de referencia** ✓: el PDF que ya estaba en el repo (`multi-tenant-refactor.pdf` ✓ → su HTML ✓) → la maqueta ahora **copia sus valores** ✓ (no los parecidos ✓): el chip de la portada ✓, el título de 42pt con la parte en **degradado** ✓, el subtítulo a 12pt ✓, las **tarjetas de métricas** ✓ (con el número en degradado ✓), el encabezado corrido (marca y documento ✓) y el pie con el número de página ✓ · la tipografía ✓ (h1 32pt ✓ · h2 **en violeta** ✓ · h4 en versalitas atenuadas ✓ · el texto a 9.5pt ✓ · el código en línea en violeta ✓ · la primera columna de las tablas en monoespaciada violeta ✓).

**Lo que la maqueta no puede copiar** ✓: el índice del PDF de la casa lleva el **número de página** de cada sección ✓ ("p. 03" ✓) → eso se sabe porque el documento está hecho hoja por hoja ✓ → en un PDF que se arma de un markdown largo **no se sabe** → el índice lleva el **número de sección** ✓ y el PDF sale con **marcadores** (el índice del lector ✓) ✓.

**Lo que aprendió la maqueta** ✓ (por lo que se veía mal ✓): el `@import` de las fuentes **va primero** ✓ (después de otra regla el navegador lo ignora ✓) · el código **no usa JetBrains Mono** (ni la web ni el del IDE: no traen los caracteres de dibujo de los árboles ✓ → el navegador cae a otra fuente → **el árbol pierde la alineación** ✓) · el resaltado **solo cuando el bloque declara su lenguaje** ✓ (un árbol de carpetas no es C# ✓) · la portada **llena la hoja** ✓.

**El manual del usuario** ✓: 25 pantallas ✓ · 19 páginas ✓ · el texto sale del **código** (los nombres de los botones y los avisos, con sus palabras exactas ✓) → y termina con **lo que hoy no se puede hacer** ✓ (el RNC del cliente no se captura en la UI ✓ → el crédito fiscal necesita un cliente con el RNC ya cargado ✓ · la venta a consumidor final de RD$250,000 o más sin RNC no se frena en la caja ✓ · no hay movimientos de inventario ✓ · no hay pantalla de categorías ✓ · nómina y asistencia no se guardan ✓ · reportes se arman en memoria ✓ · "Descargar PDF" imprime ✓ · emisores y rangos no se borran ✓ · recepción no tiene listado ✓).

**Lo que falta** ✗: las **capturas de las pantallas reales** (MP4b ✓) → el script está ✓ (`scripts/pdf/capturas.mjs` ✓) → falta que el sistema esté arriba ✓ (**el Postgres no está corriendo ✓ y su carpeta `bin` no está ✓ → no se puede arrancar desde acá ✓**).

### L6 — 2026-11-23 — los tres PDF rehechos: el documento se compone hoja por hoja

> (el dueño: *"la skill te dice como"*, *"los html ya estan escritos"*, *"estás dando muchas vueltas"*, *"termina"*)

**Lo que estaba mal** ✓ (medido con PyMuPDF sobre los tres PDF): la hoja salía en **Carta** (612×792) en vez de A4 ✗ · el **encabezado de Chrome pisaba el título** (156 casos en el back, 268 en el front ✗) · el **encuadre descentrado** (el cuerpo de 210mm dentro de una hoja de 612pt → 16.7pt de aire muerto a la derecha ✗) · **código pegado al borde del papel** (front p.10 ✗) · la portada **sin el pie** (repo · documento · fecha ✗) y el índice **sin el número de página** ✗.

**Por qué no salía A4** ✓: Playwright v1.61.1 **descarta** `paperWidth`/`paperHeight` ✗ → la hoja se quedaba en Letter ✗ → la forma correcta es `preferCSSPageSize: true` con `@page { size: 210mm 297mm }` ✓ (594.96×841.92 ✓).

**Lo que se cambió** ✓ (el pipeline, no el contenido):
- La **maqueta** (`scripts/pdf/maqueta.css` ✓) pasa a la anatomía de la casa: `@page { size: 210mm 297mm; margin: 0 }` ✓ · `body` sin padding ✓ · **una hoja = un `.page` de 210×297mm** ✓ (como `multi-tenant-refactor.html` ✓) con el aire adentro (50px 55px 55px ✓) → el texto nunca toca el papel ✓.
- El **generador** (`scripts/pdf/md-to-pdf.mjs` ✓) compone el documento en hojas **en el navegador** (el navegador es el que mide ✓): el encabezado corrido y el pie con el número van **dentro** de la hoja ✓ (no en el margen del papel ✓ → no pisan nada ✓ y la portada sale limpia ✓) · **cada sección abre hoja** ✓ · el **índice lleva el número de página** de cada sección ✓ ("p. 03" ✓ como en la casa ✓) · la portada **cuenta sus propias páginas** ✓ (el dato no se escribe a mano ✗ → no se queda viejo ✓).
- Los bloques que no entran en una hoja se **parten** (código por líneas ✓ sin perder el resaltado ✓, tablas por filas ✓, listas por partes ✓) → **0 desbordes** en los tres PDF ✓. El pipeline **falla loudly** si la composición no aplica ✓ (nunca un PDF a medio hacer ✓).

**Los tres PDF, verificados** ✓ (PyMuPDF + render a imagen ✓):

| PDF | Hojas | Tamaño | Encabezado que pisa | Texto en el borde | Índice |
|---|---|---|---|---|---|
| `Volterp/docs/guia-backend.pdf` | 74 | A4 ✓ | 0 ✓ | 0 ✓ | 31 entradas, 0 desfasadas ✓ |
| `erp-mvp/docs/guia-frontend.pdf` | 120 | A4 ✓ | 0 ✓ | 0 ✓ | 37 entradas ✓ |
| `erp-mvp/docs/manual-usuario.pdf` | 20 | A4 ✓ | 0 ✓ | 0 ✓ | 13 entradas, 0 desfasadas ✓ |

**Lo que se cuidó** ✓: el **template del back no se tocó** ✓ (el PDF se renderiza desde él, no se edita ✓) · el texto **no se pierde** al partir los bloques ✓ (prueba en el navegador: lo que queda + lo que sigue = el bloque entero ✓) · sin **mojibake** ✓ (el "Índice" se había roto al arreglar un archivo con PowerShell: `Get-Content` leyó el UTF-8 como ANSI ✗ → doble codificación ✗ → revertida ✓).

**Lo que falta** ✗: las **capturas de las pantallas reales** (MP4b ✓ → el Postgres no está corriendo ✗ → los huecos quedan marcados como "captura pendiente" ✓) · la **revisión del dueño** (MP5 ✓).

