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
| MP1 | S1, S2 | inline (parent) | PENDING | El **pipeline**: MD → HTML → PDF + la maqueta (portada, índice navegable, numeración, código con resaltado, bloques de "principio" y "stack", las páginas del PDF) → la guía es la skill `styled-pdf` |
| MP2 | S1, S3 | inline (parent) | PENDING | El **PDF del frontend** desde `FRONTEND-SKELETON-TEMPLATE.md` + lo que falte (el módulo `side-menu` nuevo ✓, la ruta `/pos` ✓, los totales de la caja ✓) |
| MP3 | S1, S3 | inline (parent) | PENDING | El **PDF del backend** desde `NET-API-SKELETON-TEMPLATE.md` + lo que falte (el readiness ✓, los permisos ✓, el e-CF ✓) |
| MP4 | S1, S4 | inline (parent) | PENDING | El **manual del usuario**: inventario de pantallas y flujos + el texto paso a paso + capturas de las pantallas reales |
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
