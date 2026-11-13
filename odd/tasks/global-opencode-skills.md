# Migración global de skills OpenCode a Gentle Shell

Migrar las skills del usuario desde OpenCode al directorio global de Pi/Gentle Shell, conservando las habilidades únicas y evitando duplicar las que Gentle Shell ya provee nativamente.

## Specs

- **S1.** "Hola champ para este proyecto es necesario que carguemos todas las skills que tengo en opencode ya que estare alternando entre ambos agentes para trabajar"
- **S2.** "tengo varias skills que desarrolle y pueden servir aqui, en opencode se cargan no se como es aqui en gentle shell"
- **S3.** "si esas 3 de anthropic tambien se van a copiar valida que las de gentle-ai no pasen porque ayer vimos que ya gentle-shell las tiene nativas, valida eso"
- **S4.** "osea estas skills no son solo para volterp"
- **S5.** "Copiar 14 y sanear (recomendado)"

## Tasks

- **T1** — S1, S3 — explorer — commit: n/a (validación read-only) — status: done — Validar inventario, frontmatter y equivalencias con las skills nativas.
- **T2** — S1, S4, S5 — inline/global-copy — commit: este commit — status: done — Copiar las 14 skills únicas al `agent-dir` global, incluidas las 3 de Anthropic.
- **T3** — S5 — inline/sanitize — commit: este commit — status: done — Reemplazar la credencial sensible en la copia de `production-deploy` sin modificar el origen.
- **T4** — S1, S2, S3, S4, S5 — inline + independent-verifier — commit: este commit — status: done — Verificar contenido, referencias, ausencia de secretos y descubrimiento de skills.
- **T5** — S1, S2, S3, S4, S5 — inline/registry — commit: este commit — status: done — Regenerar el registro de skills y documentar la activación global.

## Log

- **L1.** Hola champ para este proyecto es necesario que carguemos todas las skills que tengo en opencode ya que estare alternando entre ambos agentes para trabajar
- **L2.** tengo varias skills que desarrolle y pueden servir aqui, en opencode se cargan no se como es aqui en gentle shell
- **L3.** si esas 3 de anthropic tambien se van a copiar valida que las de gentle-ai no pasen porque ayer vimos que ya gentle-shell las tiene nativas, valida eso
- **L4.** osea es para que vivan en gentle-shell asi como en opencode ya que son mis habilidades para programar y disenar
- **L5.** no eso no tienes que portarlo muestrame el nombre de las 25 skills?
- **L6.** esta credencial de que es?
- **L7.** osea estas skills no son solo para volterp
- **L8.** Copiar 14 y sanear (recomendado)
- **L9.** Validación 2026-11-13: OpenCode contiene 25 definiciones válidas; Gentle Shell expone 15 skills nativas; hay 11 equivalencias native/OpenCode y 14 skills únicas para copiar. Se excluyen `branch-pr`, `chained-pr`, `cognitive-doc-design`, `comment-writer`, `issue-creation`, `judgment-day`, `rdd-defect-workflow`, `skill-creator`, `skill-improver`, `skill-registry` y `work-unit-commits`.
- **L10.** Destino global validado mediante `PI_CODING_AGENT_DIR`: `C:\Users\HANCER MERCEDE\AppData\Local\gentle-shell\install\agent\skills`. El destino no está limitado al repositorio Volterp.
- **L11.** Instalación atómica completada: 14 `SKILL.md` y 1.577 archivos copiados. `production-deploy` fue saneado antes de publicar la carpeta final; el origen de OpenCode quedó intacto.
- **L12.** Validación 2026-11-13 (T4): el loader de Pi reporta 14 skills globales y 0 diagnósticos; los 14 `SKILL.md` tienen frontmatter parseable (`name` + `description`), nombre conforme a `[a-z0-9-]` y descripción dentro del límite de 1024 caracteres. Escaneo de secretos sobre el payload global: 0 candidatos (el único match textual es `password: z.string()` en `senior-dev/references/security.md`, un validador Zod, no una credencial). Referencias: 0 enlaces markdown relativos rotos y 0 referencias nuevas rotas frente al origen de OpenCode; se corrigió la única referencia rota heredada (`assets/tokens.md` en `bhd-design/references/patterns.md`). Tres adaptaciones intencionales, verificadas por diff contra `~/.config/opencode/skills`: (1) `production-deploy/SKILL.md` sanea la credencial a `${POSTGRES_PASSWORD}`; (2) `bhd-design/SKILL.md` corrige la indentación del frontmatter (`version` a 2 espacios) para que el loader lo acepte; (3) `bhd-design/references/patterns.md` reescribe la referencia a `assets/tokens.md`. Los otros 12 skills son copias idénticas al origen (las 3 de Anthropic se instalaron con su nombre base, `anthropic-*` → nombre base, sin alterar su contenido) y el origen de OpenCode quedó intacto. El intento de verificación independiente fue rechazado en la admisión y ejecutó 0 tests.
- **L13.** Registro y activación 2026-11-13 (T5): `.atl/skill-registry.md` tiene 28 filas activas — 14 rutas bajo `install\agent\skills` y 14 rutas nativas bajo `node_modules\gentle-pi\skills` — y las 28 resuelven a archivos existentes. Las 14 skills globales instaladas son `bhd-design`, `canvas-design`, `frontend-design`, `gentle-ai-bench`, `go-testing`, `legacy-code`, `logo-design`, `production-deploy`, `react-no-effects`, `senior-dev`, `styled-pdf`, `systemic-issue-triage`, `theme-factory`, `verify-before-act`; el registro excluye `skill-registry` y `_shared` por diseño, y las 11 skills de OpenCode no copiadas siguen representadas por sus rutas nativas de paquete. Activación: las sesiones ya abiertas requieren `/reload` para refrescar el catálogo; las sesiones nuevas descubren automáticamente las 14 skills globales al arrancar.
