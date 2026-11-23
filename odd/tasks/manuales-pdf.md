# Manuales en PDF: para el que programa y para el que usa

Feature: `manuales-pdf`
Estado: **PLAN. Al final del trabajo.**
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
| MP1 | S1, S2 | inline (parent) | DONE | El **pipeline**: MD → HTML → PDF + la maqueta (portada, índice navegable, numeración, código con resaltado, bloques de "principio" y "stack", las páginas del PDF) → la guía es la skill `styled-pdf`. Commits `82c6daf` (la maqueta y el conversor) y `4aa5b61` (Playwright) |
| MP2 | S1, S3 | inline (parent) | DONE | El **PDF del frontend** desde `FRONTEND-SKELETON-TEMPLATE.md` → `erp-mvp/docs/guia-frontend.pdf` (85 páginas) + el link en el README |
| MP3 | S1, S3 | inline (parent) | DONE | El **PDF del backend** desde `NET-API-SKELETON-TEMPLATE.md` → `docs/guia-backend.pdf` (con la sección nueva del e-CF y del readiness ✓) + el link en el README |
| MP4 | S1, S4 | inline (parent) | DONE | El **manual del usuario**: el inventario de pantallas (25 ✓) y flujos, el texto paso a paso con los nombres exactos de los botones (sacados del código ✓) y las limitaciones que hoy tiene el sistema ✓ → `erp-mvp/docs/manual-usuario.md` ✓ → `erp-mvp/docs/manual-usuario.pdf` (19 páginas ✓, **sin números de página** ✓: se sigue paso a paso) |
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

### L4 — 2026-11-23 — la maqueta del PDF es la de la casa

> (el dueño: *"la guia-backend no lo veo bien, no me gusta mucho, como que no tiene el estilo de los manuales que yo hago: valida y dale el estilo y mejora el encuadre de las paginas"*)

**El estilo de referencia** ✓: el PDF que ya estaba en el repo (`multi-tenant-refactor.pdf` ✓ → su HTML ✓) → la maqueta ahora **copia sus valores** ✓ (no los parecidos ✓): el chip de la portada ✓, el título de 42pt con la parte en **degradado** ✓, el subtítulo a 12pt ✓, las **tarjetas de métricas** ✓ (con el número en degradado ✓), el encabezado corrido (marca y documento ✓) y el pie con el número de página ✓ · la tipografía ✓ (h1 32pt ✓ · h2 **en violeta** ✓ · h4 en versalitas atenuadas ✓ · el texto a 9.5pt ✓ · el código en línea en violeta ✓ · la primera columna de las tablas en monoespaciada violeta ✓).

**Lo que la maqueta no puede copiar** ✓: el índice del PDF de la casa lleva el **número de página** de cada sección ✓ ("p. 03" ✓) → eso se sabe porque el documento está hecho hoja por hoja ✓ → en un PDF que se arma de un markdown largo **no se sabe** → el índice lleva el **número de sección** ✓ y el PDF sale con **marcadores** (el índice del lector ✓) ✓.

**Lo que aprendió la maqueta** ✓ (por lo que se veía mal ✓): el `@import` de las fuentes **va primero** ✓ (después de otra regla el navegador lo ignora ✓) · el código **no usa JetBrains Mono** (ni la web ni el del IDE: no traen los caracteres de dibujo de los árboles ✓ → el navegador cae a otra fuente → **el árbol pierde la alineación** ✓) · el resaltado **solo cuando el bloque declara su lenguaje** ✓ (un árbol de carpetas no es C# ✓) · la portada **llena la hoja** ✓.

**El manual del usuario** ✓: 25 pantallas ✓ · 19 páginas ✓ · el texto sale del **código** (los nombres de los botones y los avisos, con sus palabras exactas ✓) → y termina con **lo que hoy no se puede hacer** ✓ (el RNC del cliente no se captura en la UI ✓ → el crédito fiscal necesita un cliente con el RNC ya cargado ✓ · la venta a consumidor final de RD$250,000 o más sin RNC no se frena en la caja ✓ · no hay movimientos de inventario ✓ · no hay pantalla de categorías ✓ · nómina y asistencia no se guardan ✓ · reportes se arman en memoria ✓ · "Descargar PDF" imprime ✓ · emisores y rangos no se borran ✓ · recepción no tiene listado ✓).

**Lo que falta** ✗: las **capturas de las pantallas reales** (MP4b ✓) → el script está ✓ (`scripts/pdf/capturas.mjs` ✓) → falta que el sistema esté arriba ✓ (**el Postgres no está corriendo ✓ y su carpeta `bin` no está ✓ → no se puede arrancar desde acá ✓**).
