# Feature: NET-API-SKELETON-TEMPLATE

## Objetivo
Crear el SKELETON + TEMPLATE que sirva como punto de partida para crear una skill de proyectos API backend .NET al estilo del usuario. NO se está escribiendo código nuevo de proyecto todavía — solo se documenta el patrón extraído de Volterp.

## Problema
Cada vez que el usuario pide un nuevo proyecto API .NET, hay que repetir las mismas explicaciones: Clean Architecture 4 capas, IRepositoryBase<T>, IServiceManager, DTOs en carpetas por entidad, Either pipeline, JWT auth, BCrypt, EF Core con Npgsql, exception middleware, rate limiting, swagger, tests con Moq/FluentAssertions. Eso es fricción.

## Por qué
Convertir el patrón de Volterp en un MD reutilizable:
- **SKELETON** = la forma (carpetas, proyectos, csproj, slnx, appsettings)
- **TEMPLATE** = qué código va en cada archivo del skeleton (con snippets reales)

Después ese MD alimenta la creación de una skill registrada en `~/.config/opencode/skills/`.

## Scope
**Incluye:**
- 5 proyectos: Domain, Application, Infrastructure, Api, Tests
- Convenciones de naming, folders, namespaces
- Snippets template de los archivos clave (AuditEntity, RepositoryBase, ServiceManager, Either pipelines, ExceptionMiddleware, etc.)
- Workflow para crear un nuevo proyecto desde el template
- Workflow para agregar una nueva entidad

**NO incluye:**
- Generador automático de proyectos (eso es fase posterior)
- Scripts `dotnet new` personalizados
- Configuración de CI/CD
- Deployment

## Constraints
- Lenguaje de artifacts: **inglés** (convención técnica del usuario)
- Lenguaje de conversación con el usuario: **español rioplatense**
- Stack fijo: .NET 10, EF Core 10, Npgsql, JWT, BCrypt, MapFlow, EitherWay, xUnit, Moq, FluentAssertions, Swashbuckle
- El MD debe ser un deliverable en sí mismo, no un prompt

## Authorized scope
Escribir 2 archivos:
1. `odd/tasks/dotnet-api-skeleton-template.md` (este)
2. `NET-API-SKELETON-TEMPLATE.md` (deliverable principal, en raíz)

## Acceptance criteria
- [x] MD tiene secciones claramente separadas: SKELETON y TEMPLATE
- [ ] SKELETON incluye: estructura de carpetas, csproj, slnx, appsettings.json base
- [ ] TEMPLATE incluye: snippet representativo de cada archivo patrón
- [ ] Cada patrón tiene: ubicación, snippet, cuándo aplicarlo
- [ ] Hay un workflow "cómo crear un proyecto nuevo desde este template"
- [ ] Hay un workflow "cómo agregar una nueva entidad"
- [ ] Convenciones de naming explícitas
- [ ] Notas sobre decisiones intencionales del usuario (MapFlow, EitherWay, InMemoryExceptionLogStore)
- [ ] Persistido en engram como decisión arquitectónica

## Task list
- [x] T1: Mapear estructura del proyecto (5 capas, ~120 archivos)
- [x] T2: Delegar análisis profundo de patrones al sub-agent de exploración
- [x] T3: Leer archivos clave para entender detalles finos (Program.cs, ServiceExtensions, RepositoryBase, CategoryService, UserService, etc.)
- [x] T4: Crear este feature doc ODD
- [ ] T5: Escribir el MD principal `NET-API-SKELETON-TEMPLATE.md` con la estructura SKELETON + TEMPLATE
- [ ] T6: Persistir la decisión en engram
- [ ] T7: Presentar al usuario y esperar feedback

## Verification
- Revisión manual del MD: ¿se entiende como punto de partida sin más contexto?
- ¿Cualquier desarrollador .NET puede tomar este MD y armar un proyecto siguiendo los snippets?
- ¿Las convenciones están explícitas y sin ambigüedad?

## Next steps
Después de que el usuario apruebe el MD:
1. Convertir el MD en una skill OpenCode registrada en `~/.config/opencode/skills/net-api-backend/`
2. Crear un script `new-dotnet-api.sh` que genere la estructura a partir del template
3. Empezar a usar la skill en el próximo proyecto API .NET que pida el usuario

## Relevant Files
- `D:\source\my_projects\Volterp\NET-API-SKELETON-TEMPLATE.md` — deliverable principal (a crear)
- `D:\source\my_projects\Volterp\odd\tasks\dotnet-api-skeleton-template.md` — este feature doc
- Volterp backend (código fuente del que se extraen los patrones)
