# .NET API Backend — Skeleton & Template

> **Propósito**: punto de partida único y obligatorio para crear cualquier proyecto API backend en .NET siguiendo el estilo del usuario. Separa **SKELETON** (la forma del proyecto) de **TEMPLATE** (qué código va en cada archivo). Después de leerlo, se puede generar un proyecto nuevo sin tener que explicar Clean Architecture, repos, servicios o auth otra vez.
>
> **Origen**: extraído del proyecto Volterp ERP backend. Multi-tenant, JWT auth, PostgreSQL, EF Core.

---

## Metadata

| Item | Value |
|---|---|
| Skill domain | .NET API backend |
| Runtime | .NET 10 |
| Architecture | Clean Architecture (4 capas + Tests) |
| Persistence | PostgreSQL + EF Core 10 (Npgsql) |
| Auth | JWT Bearer + BCrypt |
| Authorization | Mapa de rutas por controller+verb (deny-by-default) |
| Functional | EitherWay (`Either<Error, T>`) |
| Mapping | MapFlow (`IMapFrom<T>`, `IMapTo<T>`) |
| API docs | Swashbuckle.AspNetCore |
| Rate limiting | Built-in `Microsoft.AspNetCore.RateLimiting` |
| Testing | xUnit + Moq + FluentAssertions |
| Solution format | `.slnx` (no legacy `.sln`) |
| Source language | English (artifacts), Spanish (conversación) |

---

## How to read this document

- **Parte 1 — SKELETON** define la forma del proyecto: carpetas, proyectos, referencias, archivos de configuración. Es lo que se genera una vez.
- **Parte 2 — TEMPLATE** define qué código va en cada archivo del skeleton. Para cada archivo hay un snippet listo para copiar y adaptar.
- **Parte 3 — WORKFLOW** explica cómo crear un proyecto nuevo desde este template y cómo agregar una nueva entidad.
- **Parte 4 — CONVENCIONES** agrupa las reglas transversales (naming, multi-tenancy, errors, logging).

---

# PARTE 1 — SKELETON

## 1.1 Solución

Una solución `.slnx` (formato moderno XML, soportado oficialmente desde Visual Studio 2022 17.8+). Cinco proyectos, dependencias unidireccionales hacia adentro.

```
<Solution>
  <Project Path="MyApp.Api/MyApp.Api.csproj" />
  <Project Path="MyApp.Application/MyApp.Application.csproj" />
  <Project Path="MyApp.Domain/MyApp.Domain.csproj" />
  <Project Path="MyApp.Infrastructure/MyApp.Infrastructure.csproj" />
  <Project Path="MyApp.Tests/MyApp.Tests.csproj" />
</Solution>
```

## 1.2 Regla de dependencias entre proyectos

```
Api ──► Infrastructure ──► Application ──► Domain ──► ElectronicInvoicing
 │                            │              ▲              ▲
 └────────────────────────────┘              │              │
 └───────────────────────────────────────────┘──────────────┘
Tests ──► (Api, Infrastructure, Application, Domain)
```

- `Domain` no referencia a nadie **del ERP** (sí a la library fiscal, que no depende de nadie).
- `Application` solo referencia `Domain` (+ la library fiscal).
- `Infrastructure` referencia `Application` + `Domain`.
- `Api` referencia los tres.
- `Tests` referencia los cuatro (necesita acceso a todo para mockear y verificar).
- **`ElectronicInvoicing` no referencia a nadie**: es una library vendible por separado. El motor fiscal vive **por debajo** del Domain, que es lo que permite que el Domain la consuma sin que la library dependa del ERP.

## 1.3 Estructura de carpetas

```
MyApp.slnx
│
├── MyApp.Domain/
│   ├── MyApp.Domain.csproj
│   ├── Entities/        ← POCOs con comportamiento
│   ├── ValueObjects/    ← readonly record struct
│   ├── Enums/           ← estados, roles, tipos
│   └── Exceptions/      ← excepciones de dominio (NO HTTP)
│
├── MyApp.ElectronicInvoicing/    ← library fiscal vendible (no referencia al ERP)
│   ├── MyApp.ElectronicInvoicing.csproj
│   ├── Xml/             ← el modelo del documento, el writer y los perfiles por tipo
│   ├── Schemas/         ← los XSD oficiales de la DGII (EmbeddedResource)
│   ├── Signing/         ← la firma XMLDSig y el código de seguridad
│   ├── Dgii/            ← el transporte contra la DGII (semilla, token, envío, veredicto)
│   └── Qr/              ← las URLs de verificación
│
├── MyApp.Application/
│   ├── MyApp.Application.csproj
│   ├── Interfaces/      ← contratos (IRepositoryBase, IServiceManager, IServices, IRepos)
│   ├── Services/        ← implementaciones de servicios + ServiceManager
│   ├── DTOs/            ← {Entity}Dtos/{Create|Update|Response}.cs
│   ├── Helpers/         ← PaginationExtensions (+ StringExtensions solo si hace falta)
│   ├── Exceptions/      ← HTTP-aware (NotFoundException, AlreadyExistException, ValidationException)
│   │   ├── AppErrors/   ← Error record
│   │   └── {Entity}/    ← {Entity}NotFound, {Entity}AlreadyExist
│   └── Diagnostics/     ← IExceptionLogStore + impl
│
├── MyApp.Infrastructure/
│   ├── MyApp.Infrastructure.csproj
│   ├── Data/
│   │   ├── MyAppDbContext.cs
│   │   ├── Configurations/   ← IEntityTypeConfiguration por entidad
│   │   └── ValueConverters/  ← VO ⇄ columnas
│   ├── Repositories/    ← RepositoryBase<T> + repos concretos
│   ├── UnitOfWork/      ← IUnitOfWork + impl
│   ├── Services/        ← JwtService, PasswordHasher (concretos)
│   └── Migrations/      ← generadas por EF
│
├── MyApp.Api/
│   ├── MyApp.Api.csproj
│   ├── Program.cs       ← top-level, único entry point
│   ├── appsettings.json ← Jwt + ConnectionString + RateLimiting
│   ├── appsettings.Development.json
│   ├── Controllers/     ← BaseController + controllers
│   ├── Configuration/   ← Settings tipados (JwtSettings, DatabaseSettings, RateLimitingOptions)
│   ├── Extensions/      ← ExceptionMiddleWareHandler
│   ├── Helpers/         ← ServiceExtensions, Constants, CurrentUserService, PaginationParameters, HealthCheck, imports.cs
│   ├── RateLimiting/    ← RateLimiterPolicies, RateLimitPartitionResolver
│   ├── Properties/      ← launchSettings.json
│   └── wwwroot/         ← opcional, static files
│
└── MyApp.Tests/
    ├── MyApp.Tests.csproj
    ├── Domain/          ← tests de entidades y VOs
    ├── ValueObjects/    ← tests de VOs
    ├── Exceptions/      ← tests de excepciones HTTP
    ├── Services/        ← tests de application services (con Moq)
    ├── Controllers/     ← tests de autorización
    ├── Infrastructure/  ← tests de servicios concretos (Jwt, etc.)
    └── RateLimiting/    ← tests del partition resolver y wiring
```

## 1.4 csproj mínimos

### Domain

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="MapFlow" Version="1.2.0" />
  </ItemGroup>
</Project>
```

> **Razón**: Domain es la única capa que importa `MapFlow` porque las interfaces `IMapFrom<T>` viven en Domain y los DTOs (en Application) las implementan.

### Application

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\MyApp.Domain\MyApp.Domain.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="EitherWay" Version="1.2.2" />
    <PackageReference Include="MapFlow" Version="1.2.0" />
    <PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.7" />
  </ItemGroup>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <Using Include="MapFlow" />
  </ItemGroup>
</Project>
```

> **Razón**: EF Core aparece en Application porque la firma de `IRepositoryBase<T>` usa `IQueryable<T>` (que es de EF). Es la única dependencia "hacia afuera" de Application.

### Infrastructure

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\MyApp.Domain\MyApp.Domain.csproj" />
    <ProjectReference Include="..\MyApp.Application\MyApp.Application.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="BCrypt.Net-Next" Version="4.0.3" />
    <PackageReference Include="MapFlow" Version="1.2.0" />
    <PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.7" />
    <PackageReference Include="Microsoft.IdentityModel.Tokens" Version="8.0.2" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.0" />
    <PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="8.0.2" />
  </ItemGroup>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <Using Include="MapFlow" />
  </ItemGroup>
</Project>
```

### Api

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <ItemGroup>
    <ProjectReference Include="..\MyApp.Domain\MyApp.Domain.csproj" />
    <ProjectReference Include="..\MyApp.Application\MyApp.Application.csproj" />
    <ProjectReference Include="..\MyApp.Infrastructure\MyApp.Infrastructure.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="EitherWay" Version="1.2.2" />
    <PackageReference Include="MapFlow" Version="1.2.0" />
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.0" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.0">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="Swashbuckle.AspNetCore" Version="6.6.2" />
  </ItemGroup>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <NoWarn>$(NoWarn);1591</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <Using Include="MapFlow" />
  </ItemGroup>
</Project>
```

### Tests

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <Using Include="MapFlow" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="10.0.0">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="FluentAssertions" Version="6.12.0" />
    <PackageReference Include="MapFlow" Version="1.2.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.0" />
    <PackageReference Include="Moq" Version="4.20.72" />
    <PackageReference Include="xunit" Version="2.9.0" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\MyApp.Domain\MyApp.Domain.csproj" />
    <ProjectReference Include="..\MyApp.Application\MyApp.Application.csproj" />
    <ProjectReference Include="..\MyApp.Infrastructure\MyApp.Infrastructure.csproj" />
    <ProjectReference Include="..\MyApp.Api\MyApp.Api.csproj" />
  </ItemGroup>
</Project>
```

## 1.5 appsettings.json base

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=myapp;Username=myapp;Password=temppassword123;Port=5432"
  },
  "Jwt": {
    "Key": "YourSuperSecretKeyThatIsAtLeast32CharactersLong!",
    "Issuer": "MyApp.Api",
    "Audience": "MyApp.Frontend",
    "ExpiryMinutes": 60
  },
  "RateLimiting": {
    "Authenticated": { "PermitLimit": 300, "WindowSeconds": 60 },
    "Anonymous":     { "PermitLimit": 60,  "WindowSeconds": 60 },
    "Authentication":{ "PermitLimit": 5,   "WindowSeconds": 60 }
  },
  "AllowedHosts": "*"
}
```

## 1.6 launchSettings.json (Properties/)

```json
{
  "$schema": "https://json.schemastore.org/launchsettings.json",
  "profiles": {
    "http": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": true,
      "applicationUrl": "http://localhost:5106",
      "environmentVariables": { "ASPNETCORE_ENVIRONMENT": "Development" }
    },
    "https": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": true,
      "applicationUrl": "https://localhost:7007;http://localhost:5106",
      "environmentVariables": { "ASPNETCORE_ENVIRONMENT": "Development" }
    }
  }
}
```

---

# PARTE 2 — TEMPLATE

## 2.1 Domain layer

### AuditEntity (base auditable)

**Ubicación**: `MyApp.Domain/Entities/AuditEntity.cs`
**Propósito**: clase abstracta que toda entidad heredable usa. Contiene 4 campos + 3 métodos.
**Snippet:**

```csharp
namespace MyApp.Domain.Entities;

public abstract class AuditEntity
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public int? CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }

    public void MarkUpdated() { UpdatedAt = DateTime.UtcNow; }
    public void SetCreatedBy(int createdBy) { CreatedBy = createdBy; }
    public void SetUpdatedBy(int updatedBy) { UpdatedBy = updatedBy; }
}
```

### Entity (POCO con comportamiento)

**Ubicación**: `MyApp.Domain/Entities/{Entity}.cs`
**Reglas**:
- Hereda de `AuditEntity` salvo que sea una join entity.
- Propiedades públicas + `Id` como PK.
- Métodos de dominio que validan invariantes y disparan excepciones de dominio.
- Relaciones: navegación hijo→padre con `T = null!`, colección padre→hijos inicializada a `new List<T>()`.

**Snippet (con multi-tenancy):**

```csharp
using MyApp.Domain.Enums;
using MyApp.Domain.Exceptions;

namespace MyApp.Domain.Entities;

public class Category : AuditEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int CompanyId { get; set; }     // <-- multi-tenant
    public bool IsActive { get; set; } = true;

    public Company Company { get; set; } = null!;
    public ICollection<Product> Products { get; set; } = new List<Product>();

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;

    public void Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("Name cannot be empty.", nameof(newName));
        Name = newName;
    }
}
```

**Snippet (transición de estado con guard):**

```csharp
public class User : AuditEntity
{
    public UserRole Role { get; set; } = UserRole.Ventas;
    public bool IsActive { get; set; } = true;

    public void ChangeRole(UserRole newRole)
    {
        if (!IsActive)
            throw new InvalidRoleTransitionException(Role.ToString(), newRole.ToString());

        if (Role == UserRole.SuperAdmin && newRole != UserRole.SuperAdmin)
            throw new InvalidRoleTransitionException(Role.ToString(), newRole.ToString());

        Role = newRole;
    }

    public void Activate() { IsActive = true; }
    public void Deactivate() { IsActive = false; }
}
```

### ValueObject (inmutable)

**Ubicación**: `MyApp.Domain/ValueObjects/{Name}.cs`
**Reglas**:
- `readonly partial record struct`.
- Validación en el constructor (lanza excepción de dominio si falla).
- Exponer `TryParse(string?, out VO)` para uso en `Ensure()` de services — devuelve `bool` sin tirar excepción. El VO es la **única fuente de verdad** para la validación; nunca se duplica la lógica en `StringExtensions` u otra utilidad.
- Sin setters públicos.

**Snippet:**

```csharp
using System.Text.RegularExpressions;
using MyApp.Domain.Exceptions;

namespace MyApp.Domain.ValueObjects;

public readonly partial record struct Email
{
    [GeneratedRegex(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$")]
    private static partial Regex EmailRegex();

    public string Value { get; }

    public Email(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidEmailException(value);

        if (!EmailRegex().IsMatch(value))
            throw new InvalidEmailException(value);

        Value = value;
    }

    // Constructor privado sin validación — usado solo por TryParse para evitar validar dos veces.
    private Email(string value, bool _)
    {
        Value = value;
    }

    /// <summary>
    /// Validación "soft" para usar dentro de <c>Ensure()</c> en services.
    /// Devuelve true/false sin lanzar excepción.
    /// </summary>
    public static bool TryParse(string? value, out Email email)
    {
        if (!string.IsNullOrWhiteSpace(value) && EmailRegex().IsMatch(value))
        {
            email = new Email(value, false);
            return true;
        }
        email = default;
        return false;
    }

    public override string ToString() => Value;
}
```

### Enum

**Ubicación**: `MyApp.Domain/Enums/{Concept}.cs`
**Reglas**:
- Valores numéricos explícitos (no dejar que el compilador decida).
- Nombres en PascalCase, sin prefijo.

**Snippet:**

```csharp
namespace MyApp.Domain.Enums;

public enum UserRole
{
    SuperAdmin = 1,
    Admin = 2,
    Ventas = 3,
    Inventario = 4,
    Contabilidad = 5,
    RRHH = 6
}
```

> **Configurar `JsonStringEnumConverter` en `ServiceExtensions.ConfigureControllers` para que la API serialice enums como string.**

### Domain Exception

**Ubicación**: `MyApp.Domain/Exceptions/{Concept}Exception.cs`
**Reglas**:
- Hereda de `Exception`.
- Expone propiedades con datos del error (Requested, FromStatus, etc.).
- NO implementa `IHttpException` — eso es responsabilidad de Application.

**Snippet:**

```csharp
namespace MyApp.Domain.Exceptions;

public class InsufficientStockException : Exception
{
    public int Requested { get; }
    public int Available { get; }

    public InsufficientStockException(int requested, int available)
        : base($"Insufficient stock. Requested: {requested}, Available: {available}")
    {
        Requested = requested;
        Available = available;
    }
}
```

---

## 2.2 Application layer

### IRepositoryBase<T>

**Ubicación**: `MyApp.Application/Interfaces/IRepositoryBase.cs`
**Reglas**:
- Métodos CRUD genéricos con paginación.
- `Func<IQueryable<T>, IQueryable<T>>? includes` permite eager loading opcional.
- **Nota histórica**: el proyecto usa `GetByCondictionsAsync` (typo) — mantenerlo por consistencia.

**Snippet:**

```csharp
using System.Linq.Expressions;
using MyApp.Application.Helpers;

namespace MyApp.Application.Interfaces;

public interface IRepositoryBase<T> where T : class
{
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<PagedResult<T>> GetAllAsync(Expression<Func<T, bool>> predicate,
        int pageNumber, int pageSize,
        CancellationToken ct = default,
        Func<IQueryable<T>, IQueryable<T>>? includes = null);
    Task<T?> GetByCondictionsAsync(Expression<Func<T, bool>> predicate,
        CancellationToken ct = default,
        Func<IQueryable<T>, IQueryable<T>>? includes = null);
    Task AddAsync(T entity, CancellationToken ct = default);
    Task UpdateAsync(T entity, CancellationToken ct = default);
    Task DeleteAsync(T entity, CancellationToken ct = default);
    Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
}
```

### IUnitOfWork

**Ubicación**: `MyApp.Application/Interfaces/IUnitOfWork.cs`
**Reglas**:
- Expone cada repo como propiedad.
- `CommitAsync(ct)` único punto de `SaveChangesAsync`.
- `Dispose()` delega al DbContext.

**Snippet:**

```csharp
namespace MyApp.Application.Interfaces;

public interface IUnitOfWork
{
    IUserRepository Users { get; }
    ICategoryRepository Categories { get; }
    // ... una por entidad

    Task<int> CommitAsync(CancellationToken ct = default);
    void Dispose();
}
```

### IService<T> (interface de servicio CRUD)

**Ubicación**: `MyApp.Application/Interfaces/I{Name}Service.cs`
**Reglas**:
- Todas las operaciones (excepto GetAll) devuelven `Task<Either<Error, T>>`.
- `GetAll` devuelve `Task<PagedResult<TDto>>` directamente.
- `GetById` devuelve `Task<Either<Error, TDto?>>` (puede no existir).
- `Delete` devuelve `Task<Either<Error, Unit>>`.
- Multi-tenant: pasar `companyId` por método (no asumir del contexto).

**Snippet:**

```csharp
using EitherWay;
using MyApp.Application.DTOs.CategoryDtos;
using MyApp.Application.Exceptions.AppErrors;
using MyApp.Application.Helpers;

namespace MyApp.Application.Interfaces;

public interface ICategoryService
{
    Task<PagedResult<CategoryDto>> GetAllAsync(int companyId, int pageNumber, int pageSize, CancellationToken ct = default);
    Task<Either<Error, CategoryDto?>> GetByIdAsync(int id, int companyId, CancellationToken ct = default);
    Task<Either<Error, CategoryDto>> CreateAsync(CreateCategoryRequest request, int companyId, int? userId, CancellationToken ct = default);
    Task<Either<Error, CategoryDto>> UpdateAsync(int id, UpdateCategoryRequest request, int companyId, int? userId, CancellationToken ct = default);
    Task<Either<Error, Unit>> DeleteAsync(int id, int companyId, CancellationToken ct = default);
    Task<bool> ExistsAsync(int id, CancellationToken ct = default);
}
```

### Service<T> (implementación con Either pipeline)

**Ubicación**: `MyApp.Application/Services/{Name}Service.cs`
**Reglas**:
- Primary ctor con `IUnitOfWork` + dependencias cross-cutting (`IPasswordHasher`, `ICurrentUserService`, etc.).
- Pipeline Either: `FromRight` → `Ensure` (validaciones) → `Map` (proyecciones) → `FlatMap` (operaciones async con commit) → `Map` (a DTO) → `Run`.
- Para mutaciones, setear audit fields (`SetCreatedBy`, `SetUpdatedBy`) ANTES del `CommitAsync`.

**Snippet (CreateAsync — pipeline Either):**

```csharp
using EitherWay;
using MyApp.Application.DTOs.CategoryDtos;
using MyApp.Application.Exceptions.AppErrors;
using MyApp.Application.Helpers;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;

namespace MyApp.Application.Services;

public class CategoryService(IUnitOfWork unitOfWork) : ICategoryService
{
    public async Task<Either<Error, CategoryDto>> CreateAsync(
        CreateCategoryRequest request, int companyId, int? userId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, CreateCategoryRequest>
            .FromRight(request)
            .Ensure(r => !string.IsNullOrWhiteSpace(r.Name), new Error("The name is required."))
            .Ensure(_ => companyId > 0, new Error("companyId must be greater than zero."))
            .Map(r => r.Project())
            .Map(category =>
            {
                category.CompanyId = companyId;
                if (userId.HasValue) category.SetCreatedBy(userId.Value);
                return category;
            })
            .FlatMap(async category =>
            {
                await unitOfWork.Categories.AddCategoryAsync(category, ct);
                await unitOfWork.CommitAsync(ct);
                return category;
            }, ex => new Error(ex.Message))
            .Map(category => category.MapTo<Category, CategoryDto>())
            .Run();
    }
}
```

**Snippet (UpdateAsync — apply sobre entidad existente):**

```csharp
public async Task<Either<Error, CategoryDto>> UpdateAsync(
    int id, UpdateCategoryRequest request, int companyId, int? userId, CancellationToken ct = default)
{
    return await EitherAsync<Error, int>
        .FromRight(id)
        .Ensure(x => x > 0, new Error("id must be greater than zero."))
        .Ensure(_ => companyId > 0, new Error("companyId must be greater than zero."))
        .FlatMap(async categoryId => await unitOfWork.Categories.GetCategoryByIdAsync(categoryId, ct),
            ex => new Error(ex.Message))
        .Ensure(category => category is not null && category.CompanyId == companyId,
            new Error("Category not found."))
        .Map(category =>
        {
            category.Apply(x =>
            {
                x.Name = request.Name;
                x.Description = request.Description;
                x.IsActive = request.IsActive;
                x.UpdatedAt = DateTime.UtcNow;
                if (userId.HasValue) x.SetUpdatedBy(userId.Value);
            });
            return category;
        })
        .FlatMap(async category =>
        {
            await unitOfWork.Categories.UpdateCategoryAsync(category, ct);
            await unitOfWork.CommitAsync(ct);
            return category;
        }, ex => new Error(ex.Message))
        .Map(category => category.MapTo<Category, CategoryDto>())
        .Run();
}
```

**Snippet (UpdateAsync con validación de VO via TryParse):**

```csharp
public async Task<Either<Error, UserDto>> UpdateAsync(
    int id, UserWithPasswordHashDto request, int? userId, CancellationToken ct = default)
{
    return await EitherAsync<Error, int>
        .FromRight(id)
        .Ensure(x => x > 0, new Error("id must be greater than zero."))
        .Ensure(_ => Email.TryParse(request.Email, out _), new Error("the email address is not valid"))
        .FlatMap(async userId => await unitOfWork.Users.GetUserByIdAsync(userId, ct),
            ex => new Error(ex.Message))
        .Ensure(user => user is not null, new Error("user not found"))
        .Map(user =>
        {
            return user.Apply(x =>
            {
                x.Email = request.Email;
                x.FullName = request.FullName;
                x.Role = request.Role;
                x.IsActive = request.IsActive;
                x.UpdatedAt = DateTime.UtcNow;
                if (userId.HasValue) x.SetUpdatedBy(userId.Value);
            });
        })
        .FlatMap(async user =>
        {
            await unitOfWork.Users.UpdateUserAsync(user, ct);
            await unitOfWork.CommitAsync(ct);
            return user;
        }, ex => new Error($"Failed to update user: {ex.Message}"))
        .Map(user => user.MapTo<User, UserDto>())
        .Run();
}
```

### IServiceManager (facade)

**Ubicación**: `MyApp.Application/Interfaces/IServiceManager.cs` + `Services/ServiceManager.cs`
**Reglas**:
- Una propiedad por servicio.
- Lazy init con `??=` (o `Lazy<T>` para Infrastructure si querés).
- **La fachada expone todo lo que los controllers consumen**: un controller solo recibe `IServiceManager` y pide sus servicios por ahí. Un controller que inyecta un servicio (o un repositorio) se salta la fachada.
- **Los servicios no se registran en el DI**: el DI registra **solo** el `ServiceManager` (y las excepciones de abajo).
- **La excepción**: un servicio que es un **seam** (un puerto hacia afuera: la DGII, el correo, un cliente HTTP) o que tiene **dependencias con ciclo de vida propio en el DI** (singletons, typed clients, background services) **no se puede armar con `new`** → se registra en el DI, se **inyecta en el constructor** del `ServiceManager` y la fachada lo expone igual: el controller lo sigue pidiendo por la fachada.

**Snippet interface:**

```csharp
namespace MyApp.Application.Interfaces;

public interface IServiceManager
{
    IProductService Products { get; }
    ICategoryService Categories { get; }
    IUserService Users { get; }
    ICompanyService Companies { get; }
    // ...
}
```

**Snippet impl:**

```csharp
using MyApp.Application.Interfaces;

namespace MyApp.Application.Services;

public class ServiceManager(IUnitOfWork unitOfWork, IPasswordHasher passwordHasher, ICurrentUserService currentUserService)
    : IServiceManager
{
    private IProductService? _products;
    public IProductService Products => _products ??= new ProductService(unitOfWork);

    private IUserService? _users;
    public IUserService Users => _users ??= new UserService(unitOfWork, passwordHasher, currentUserService);

    // ... una por servicio

    // La excepción: el seam se inyecta y la fachada lo expone igual.
    public IElectronicInvoiceService ElectronicInvoices => electronicInvoices;
}
```

### DTOs — convención por entidad

**Ubicación**: `MyApp.Application/DTOs/{Entity}Dtos/`
**Reglas**:
- Una carpeta por entidad.
- Tres archivos estándar:
  - `{Entity}Dto` — response, implementa `IMapFrom<{Entity}>` con `MapFrom(source)`.
  - `Create{Name}Request` — input de creación, implementa `IMapTo<{Entity}>` con `MapTo()`.
  - `Update{Name}Request` — input de update, implementa `IMapTo<{Entity}>`.
- Records posicionales con primary ctor.
- DTOs especializados (ej. `UserWithPasswordHashDto`) van en archivos separados, NO mezclados.

**Snippet response (IMapFrom):**

```csharp
using MyApp.Domain.Entities;

namespace MyApp.Application.DTOs.CategoryDtos;

public record CategoryDto : IMapFrom<Category>
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int CompanyId { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }

    public void MapFrom(Category source)
    {
        Id = source.Id;
        Name = source.Name;
        Description = source.Description;
        CompanyId = source.CompanyId;
        IsActive = source.IsActive;
        CreatedAt = source.CreatedAt;
    }
}
```

**Snippet create request (IMapTo):**

```csharp
using MyApp.Domain.Entities;

namespace MyApp.Application.DTOs.CategoryDtos;

public record CreateCategoryRequest(
    string Name,
    string? Description,
    int CompanyId
) : IMapTo<Category>
{
    public Category MapTo()
    {
        return new Category
        {
            Name = Name,
            Description = Description,
            CompanyId = CompanyId
        };
    }
}
```

**Snippet update request:**

```csharp
using MyApp.Domain.Entities;

namespace MyApp.Application.DTOs.CategoryDtos;

public record UpdateCategoryRequest(
    string Name,
    string? Description,
    bool IsActive
) : IMapTo<Category>
{
    public Category MapTo()
    {
        return new Category
        {
            Name = Name,
            Description = Description,
            IsActive = IsActive
        };
    }
}
```

**AuditableDto (base opcional para DTOs auditables):**

```csharp
namespace MyApp.Application.DTOs;

public abstract record AuditableDto
{
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public int? CreatedBy { get; init; }
    public int? UpdatedBy { get; init; }
}
```

**Auth DTOs (siempre compartidos):**

```csharp
namespace MyApp.Application.DTOs;

public record LoginRequest(string Username, string Password);
public record LoginResponse(string Token, string Username, string Email, string FullName, string Role, int CompanyId);
public record ErrorResponse(string Error, string? Details = null);
```

### Exceptions base (HTTP-aware)

**Ubicación**: `MyApp.Application/Exceptions/`

**`IHttpException`** (contrato — `Interfaces/`):
```csharp
namespace MyApp.Application.Interfaces;

public interface IHttpException
{
    int HttpStatusCode { get; }
}
```

**`NotFoundException`** (404):
```csharp
using MyApp.Application.Interfaces;

namespace MyApp.Application.Exceptions;

public class NotFoundException : Exception, IHttpException
{
    public int HttpStatusCode => 404;
    public int ErrorCode { get; }
    public object? Values { get; }

    public NotFoundException() { }
    public NotFoundException(string? message) : base(message) { }
    public NotFoundException(string? message, object? values) : base(message) { Values = values; }
    public NotFoundException(string? message, object? values, int errorCode) : base(message)
    {
        Values = values;
        ErrorCode = errorCode;
    }
    public NotFoundException(string? message, Exception? innerException) : base(message, innerException) { }
}
```

**`AlreadyExistException`** (409) — mismo shape que NotFound pero `HttpStatusCode => 409`.

**Subclases por entidad** (`Exceptions/{Entity}/{Entity}NotFoundException.cs`):
```csharp
namespace MyApp.Application.Exceptions.User;

public class UserNotFoundException(string message) : NotFoundException(message);
```

```csharp
namespace MyApp.Application.Exceptions.User;

public class UserAlreadyExistException(string message) : AlreadyExistException(message);
```

**`ValidationException`** (422):
```csharp
using MyApp.Application.Interfaces;

namespace MyApp.Application.Exceptions;

public class ValidationExecption : Exception, IHttpException
{
    public int HttpStatusCode => 422;
    public int ErrorCode { get; }
    public object _values { get; }

    public override string Message => base.Message;

    public ValidationExecption(string message, object info) : base(message) { _values = info; }
    public ValidationExecption(string message, string info, int errorCode) : base(message)
    {
        ErrorCode = errorCode;
        _values = info;
    }
}
```

**`Error`** (record compartido en `Exceptions/AppErrors/`):
```csharp
namespace MyApp.Application.Exceptions.AppErrors;

public record Error(string Message);
```

### Helpers

**Ubicación**: `MyApp.Application/Helpers/`

**PaginationExtensions.cs**:
```csharp
using Microsoft.EntityFrameworkCore;

namespace MyApp.Application.Helpers;

public static class PaginationExtensions
{
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> source, int pageNumber, int pageSize, CancellationToken token)
        where T : class
    {
        var rowCount = await source.CountAsync(token);
        var pageCount = (int)Math.Ceiling(rowCount / (double)pageSize);
        var items = await source.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(token);

        return new PagedResult<T>
        {
            RowCount = rowCount,
            PageNumber = pageNumber,
            PageSize = pageSize,
            PageCount = pageCount,
            Items = items
        };
    }
}
```

**StringExtensions.cs** — NO incluir en el template por defecto. La validación de tipos con semántica propia (emails, tax IDs, formatos) vive en el VO via `TryParse`. `StringExtensions` solo se justifica para operaciones de string sin un VO asociado (slugify, normalización, trimming, etc.). Si en algún caso particular se necesita una extensión de validación, revisar primero si corresponde crear/mejorar el VO.

> **Regla**: nunca duplicar lógica de validación de un VO en `StringExtensions`. El VO es la única fuente de verdad.

### Cross-cutting interfaces

**Ubicación**: `MyApp.Application/Interfaces/`

```csharp
public interface IJwtService
{
    string GenerateToken(string username, string email, string role, int companyId, int userId);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public interface ICurrentUserService
{
    bool IsSuperAdmin();
    int GetCompanyId();
    int? GetUserId();
}
```

### Diagnostics

**Ubicación**: `MyApp.Application/Diagnostics/`

**IExceptionLogStore.cs**:
```csharp
namespace MyApp.Application.Diagnostics;

public interface IExceptionLogStore
{
    void Record(Exception exception, int statusCode, string traceId, string path, string? detail);
    IReadOnlyList<ExceptionLogEntry> GetRecent(int count = 20);
}
```

**ExceptionLogEntry.cs**:
```csharp
namespace MyApp.Application.Diagnostics;

public class ExceptionLogEntry
{
    public DateTime Timestamp { get; init; }
    public string ExceptionType { get; init; } = "";
    public string Message { get; init; } = "";
    public int StatusCode { get; init; }
    public string TraceId { get; init; } = "";
    public string Path { get; init; } = "";
    public string? Detail { get; init; }
}
```

**InMemoryExceptionLogStore.cs** (singleton con ring buffer):
```csharp
using System.Collections.Concurrent;

namespace MyApp.Application.Diagnostics;

public class InMemoryExceptionLogStore : IExceptionLogStore
{
    private readonly ConcurrentQueue<ExceptionLogEntry> _queue = new();
    private readonly int _capacity;

    public InMemoryExceptionLogStore(int capacity = 100) { _capacity = capacity; }

    public void Record(Exception exception, int statusCode, string traceId, string path, string? detail)
    {
        var entry = new ExceptionLogEntry
        {
            Timestamp = DateTime.UtcNow,
            ExceptionType = exception.GetType().Name,
            Message = exception.Message,
            StatusCode = statusCode,
            TraceId = traceId,
            Path = path,
            Detail = detail
        };
        _queue.Enqueue(entry);
        while (_queue.Count > _capacity && _queue.TryDequeue(out _)) { }
    }

    public IReadOnlyList<ExceptionLogEntry> GetRecent(int count = 20)
        => _queue.Reverse().Take(count).ToList().AsReadOnly();
}
```

---

## 2.3 Infrastructure layer

### RepositoryBase<T>

**Ubicación**: `MyApp.Infrastructure/Repositories/RepositoryBase.cs`
**Reglas**:
- Primary ctor con `MyAppDbContext`.
- `Set()` helper que devuelve `context.Set<T>()`.
- `GetAllAsync` aplica `AsNoTracking()` y llama a `ToPagedResultAsync`.

**Snippet:**

```csharp
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MyApp.Application.Helpers;
using MyApp.Application.Interfaces;
using MyApp.Infrastructure.Data;

namespace MyApp.Infrastructure.Repositories;

public abstract class RepositoryBase<T>(MyAppDbContext context) : IRepositoryBase<T>
    where T : class
{
    protected DbSet<T> Set() => context.Set<T>();

    public virtual async Task<T?> GetByIdAsync(int id, CancellationToken ct = default)
        => await Set().FindAsync([id], ct);

    public virtual async Task<PagedResult<T>> GetAllAsync(Expression<Func<T, bool>> predicate,
        int pageNumber, int pageSize,
        CancellationToken ct = default,
        Func<IQueryable<T>, IQueryable<T>>? includes = null)
    {
        var query = Set().Where(predicate).AsNoTracking();
        if (includes is not null) query = includes(query);
        return await query.ToPagedResultAsync(pageNumber, pageSize, ct);
    }

    public async Task<T?> GetByCondictionsAsync(Expression<Func<T, bool>> predicate,
        CancellationToken ct = default,
        Func<IQueryable<T>, IQueryable<T>>? includes = null)
    {
        var query = Set().Where(predicate).AsNoTracking();
        if (includes is not null) query = includes(query);
        return await query.FirstOrDefaultAsync(ct);
    }

    public virtual async Task AddAsync(T entity, CancellationToken ct = default)
        => await Set().AddAsync(entity, ct);

    public virtual async Task UpdateAsync(T entity, CancellationToken ct = default)
        => await Task.FromResult(Set().Update(entity));

    public virtual async Task DeleteAsync(T entity, CancellationToken ct = default)
        => await Task.FromResult(Set().Remove(entity));

    public virtual async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => await Set().AnyAsync(predicate, ct);
}
```

### Repository concreto (extiende RepositoryBase)

**Ubicación**: `MyApp.Infrastructure/Repositories/{Name}Repository.cs`
**Reglas**:
- Hereda de `RepositoryBase<T>` e implementa `I{Name}Repository`.
- Métodos específicos de la entidad usan internamente los métodos del base.

**Snippet (CategoryRepository):**

```csharp
using Microsoft.EntityFrameworkCore;
using MyApp.Application.Helpers;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;
using MyApp.Infrastructure.Data;

namespace MyApp.Infrastructure.Repositories;

public class CategoryRepository(MyAppDbContext context)
    : RepositoryBase<Category>(context), ICategoryRepository
{
    public async Task<Category?> GetCategoryByIdAsync(int id, CancellationToken ct = default)
        => await GetByCondictionsAsync(c => c.Id == id, ct);

    public async Task<PagedResult<Category>> GetAllCategoriesByCompanyAsync(
        int companyId, int pageNumber, int pageSize, CancellationToken ct = default)
        => await GetAllAsync(c => c.CompanyId == companyId, pageNumber, pageSize, ct);

    public async Task<Category> AddCategoryAsync(Category category, CancellationToken ct = default)
    {
        await AddAsync(category, ct);
        return category;
    }

    public async Task UpdateCategoryAsync(Category category, CancellationToken ct = default)
        => await UpdateAsync(category, ct);

    public async Task DeleteCategoryAsync(int id, CancellationToken ct = default)
    {
        var category = await GetByIdAsync(id, ct);
        if (category is not null)
            await DeleteAsync(category, ct);
    }

    public async Task<bool> ExistsCategoryAsync(int id, CancellationToken ct = default)
        => await ExistsAsync(c => c.Id == id, ct);
}
```

### UnitOfWork (impl)

**Ubicación**: `MyApp.Infrastructure/UnitOfWork/UnitOfWork.cs`

```csharp
using MyApp.Application.Interfaces;
using MyApp.Infrastructure.Data;
using MyApp.Infrastructure.Repositories;

namespace MyApp.Infrastructure.UnitOfWork;

public class UnitOfWork(MyAppDbContext context) : IUnitOfWork
{
    private readonly Lazy<IUserRepository> _users = new(() => new UserRepository(context));
    private readonly Lazy<ICategoryRepository> _categories = new(() => new CategoryRepository(context));
    // ... una Lazy<T> por repo

    public IUserRepository Users => _users.Value;
    public ICategoryRepository Categories => _categories.Value;

    public async Task<int> CommitAsync(CancellationToken ct = default) => await context.SaveChangesAsync(ct);
    public void Dispose() => context.Dispose();
}
```

### DbContext

**Ubicación**: `MyApp.Infrastructure/Data/MyAppDbContext.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using MyApp.Domain.Entities;

namespace MyApp.Infrastructure.Data;

public class MyAppDbContext(DbContextOptions<MyAppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Product> Products => Set<Product>();
    // ... un DbSet<T> por entidad

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MyAppDbContext).Assembly);
    }
}
```

### IEntityTypeConfiguration<T>

**Ubicación**: `MyApp.Infrastructure/Data/Configurations/{Entity}Configuration.cs`
**Reglas**:
- Una clase por entidad.
- PK, índices únicos (por tenant cuando aplique), longitudes, FK con `OnDelete(DeleteBehavior.Restrict)`.
- Datos seed cuando aplique (admin inicial, etc.).

**Snippet (Category — índice compuesto único + FK restrict):**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyApp.Domain.Entities;

namespace MyApp.Infrastructure.Data.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> entity)
    {
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => new { e.Name, e.CompanyId }).IsUnique();   // único por tenant
        entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
        entity.Property(e => e.Description).HasMaxLength(200);

        entity.HasOne(e => e.Company)
            .WithMany(c => c.Categories)
            .HasForeignKey(e => e.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

**Snippet (User con seed inicial):**

```csharp
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> entity)
    {
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => e.Username).IsUnique();
        entity.HasIndex(e => e.Email).IsUnique();
        entity.Property(e => e.Username).IsRequired().HasMaxLength(50);
        entity.Property(e => e.Email).IsRequired().HasMaxLength(100);
        entity.Property(e => e.PasswordHash).IsRequired();
        entity.Property(e => e.Role).IsRequired().HasMaxLength(30);
        entity.Property(e => e.FullName).IsRequired().HasMaxLength(100);

        entity.HasOne(e => e.Company)
              .WithMany(c => c.Users)
              .HasForeignKey(e => e.CompanyId)
              .OnDelete(DeleteBehavior.Restrict);

        entity.HasData(new User
        {
            Id = 1,
            Username = "admin",
            PasswordHash = "$2a$12$....",   // hash BCrypt precomputado
            Email = "admin@example.com",
            FullName = "Administrador",
            Role = UserRole.SuperAdmin,
            CompanyId = 1,
            IsActive = true,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
    }
}
```

### ValueConverter

**Ubicación**: `MyApp.Infrastructure/Data/ValueConverters/{Name}Converter.cs`

```csharp
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MyApp.Domain.ValueObjects;

namespace MyApp.Infrastructure.Data.ValueConverters;

public class EmailConverter : ValueConverter<Email, string>
{
    public EmailConverter()
        : base(
            email => email.Value,
            value => new Email(value))
    {
    }
}
```

> Usar `ValueConverter` cuando el VO tiene 1 sola columna interna (string). Para VOs compuestos (Money con Amount + Currency) usar `ComplexProperty` en la configuration.

### JwtService (impl)

**Ubicación**: `MyApp.Infrastructure/Services/JwtService.cs`

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MyApp.Application.Interfaces;

namespace MyApp.Infrastructure.Services;

public class JwtService(IConfiguration configuration) : IJwtService
{
    public string GenerateToken(string username, string email, string role, int companyId, int userId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Role, role),
            new Claim("companyId", companyId.ToString()),
            new Claim("userId", userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(int.Parse(configuration["Jwt:ExpiryMinutes"]!)),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
```

### PasswordHasher (impl)

**Ubicación**: `MyApp.Infrastructure/Services/PasswordHasher.cs`

```csharp
using System.Security.Cryptography;
using System.Text;
using MyApp.Application.Interfaces;

namespace MyApp.Infrastructure.Services;

public class PasswordHasher : IPasswordHasher
{
    private const string BcryptPrefix = "$2";

    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, 12);

    public bool Verify(string password, string hash)
    {
        if (string.IsNullOrEmpty(hash)) return false;

        if (hash.StartsWith(BcryptPrefix))
            return BCrypt.Net.BCrypt.Verify(password, hash);

        return LegacyVerify(password, hash);
    }

    private static bool LegacyVerify(string password, string hash)
    {
        if (password == hash) return true;

        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        var computedHash = Convert.ToBase64String(bytes);
        return computedHash == hash;
    }
}
```

> **Por qué BCrypt cost 12 + fallback legacy**: BCrypt es estándar moderno (cost12 = ~250ms por hash en hardware 2024). El prefijo `"$2"` distingue hashes BCrypt de hashes SHA256 legacy. La rama legacy permite migrar gradualmente.

---

## 2.4 API layer

### Program.cs

**Ubicación**: `MyApp.Api/Program.cs`
**Reglas**:
- Top-level statements.
- Settings binding manual desde `appsettings.json` (sin `IOptions<T>` para Jwt/Database).
- DI por método estático (`ServiceExtensions.ConfigureXxx`).
- Migración automática al arrancar.
- Orden del pipeline (de estricto a flexible):
  1. Exception handler
  2. Forwarded headers
  3. CORS
  4. Authentication
  5. Rate limiter
  6. Authorization
  7. Swagger (solo Development)
  8. Controllers
  9. Health check
  10. Diagnostics
  11. Static files

**Snippet:**

```csharp
using MyApp.Api.Configuration;
using MyApp.Api.Helpers;
using MyApp.Application.Diagnostics;
using MyApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using MyApp.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

var jwtSettings = new JwtSettings
{
    Key = builder.Configuration["Jwt:Key"]!,
    Issuer = builder.Configuration["Jwt:Issuer"]!,
    Audience = builder.Configuration["Jwt:Audience"]!,
    ExpiryMinutes = int.Parse(builder.Configuration["Jwt:ExpiryMinutes"]!)
};

var dbSettings = new DatabaseSettings
{
    DefaultConnection = builder.Configuration.GetConnectionString("DefaultConnection")!
};

ServiceExtensions.ConfigureDbContext(builder.Services, dbSettings);
ServiceExtensions.ConfigureUnitOfWork(builder.Services);
ServiceExtensions.ConfigureServiceManager(builder.Services);
ServiceExtensions.ConfigureJwt(builder.Services, jwtSettings);
ServiceExtensions.ConfigureControllers(builder.Services);
ServiceExtensions.ConfigureCors(builder.Services);
ServiceExtensions.ConfigureExceptionLogStore(builder.Services);
ServiceExtensions.ConfigureCurrentUser(builder.Services);
builder.Services.ConfigureForwardedHeaders();
builder.Services.ConfigureRateLimiting(builder.Configuration);
builder.Services.ConfigureSwagger();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MyAppDbContext>();
    db.Database.Migrate();
}

app.ConfigureExceptionMiddleWare(app.Logger);
app.UseForwardedHeaders();
app.UseCors();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "MyApp API v1");
        app.MapGet("/", () => Results.Redirect("/swagger"));
    });
}

app.MapControllers();
app.MapGet("health", async (MyAppDbContext db) =>
{
    var checks = await HealthCheck.Init(db);
    var allHealthy = checks.All(c => c.GetType().GetProperty("status")?.GetValue(c)?.ToString() == "healthy");
    return Results.Ok(new { status = allHealthy ? "healthy" : "degraded", timestamp = DateTime.UtcNow, checks });
}).DisableRateLimiting();
app.MapGet("/_diagnostics/exceptions", (IExceptionLogStore store) => Results.Ok(store.GetRecent(50)));
app.UseStaticFiles();
app.Run();
```

### ServiceExtensions (registro DI por sección)

**Ubicación**: `MyApp.Api/Helpers/ServiceExtensions.cs`
**Reglas**: un método estático `Configure*` por responsabilidad.

```csharp
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MyApp.Api.Configuration;
using MyApp.Api.RateLimiting;
using MyApp.Application.Diagnostics;
using MyApp.Application.Interfaces;
using MyApp.Application.Services;
using MyApp.Infrastructure.Data;
using MyApp.Infrastructure.Services;
using MyApp.Infrastructure.UnitOfWork;

namespace MyApp.Api.Helpers;

public static class ServiceExtensions
{
    public static void ConfigureDbContext(IServiceCollection services, DatabaseSettings settings)
        => services.AddDbContext<MyAppDbContext>(options =>
            options.UseNpgsql(settings.DefaultConnection));

    public static void ConfigureUnitOfWork(IServiceCollection services)
        => services.AddScoped<IUnitOfWork, UnitOfWork>();

    public static void ConfigureServiceManager(IServiceCollection services)
        => services.AddScoped<IServiceManager, ServiceManager>();

    public static void ConfigureJwt(IServiceCollection services, JwtSettings jwtSettings)
    {
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidAudience = jwtSettings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key))
                };
            });

        services.AddAuthorization();
    }

    public static void ConfigureControllers(IServiceCollection services)
        => services.AddControllers()
            .AddJsonOptions(options =>
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

    public static void ConfigureCors(IServiceCollection services)
        => services.AddCors(options =>
            options.AddDefaultPolicy(policy =>
                policy.WithOrigins("http://localhost:5173")
                      .AllowAnyHeader()
                      .AllowAnyMethod()));

    public static void ConfigureExceptionLogStore(IServiceCollection services)
        => services.AddSingleton<IExceptionLogStore>(new InMemoryExceptionLogStore(100));

    public static void ConfigureCurrentUser(IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
    }

    public static void ConfigureForwardedHeaders(this IServiceCollection services)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            if (!options.KnownProxies.Contains(IPAddress.Loopback))
                options.KnownProxies.Add(IPAddress.Loopback);
            if (!options.KnownProxies.Contains(IPAddress.IPv6Loopback))
                options.KnownProxies.Add(IPAddress.IPv6Loopback);
        });
    }

    public static void ConfigureSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "MyApp API", Version = "v1", Description = Constants.Description });

            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT"
            });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() }
            });

            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath)) c.IncludeXmlComments(xmlPath);
        });
    }
}
```

### imports.cs (global usings del API)

**Ubicación**: `MyApp.Api/Helpers/imports.cs`

```csharp
global using EitherWay;
global using Microsoft.AspNetCore.Mvc;
global using Microsoft.AspNetCore.RateLimiting;
global using MyApp.Api.Helpers;
global using MyApp.Api.RateLimiting;
global using MyApp.Application.DTOs;
global using MyApp.Application.DTOs.UserDtos;
global using MyApp.Application.Exceptions.AppErrors;
global using MyApp.Application.Interfaces;
global using Microsoft.AspNetCore.Authorization;
global using System.Security.Claims;
```

### BaseController

**Ubicación**: `MyApp.Api/Controllers/BaseController.cs`

```csharp
namespace MyApp.Api.Controllers;

public abstract class BaseController : ControllerBase
{
    protected int GetCurrentUserCompanyId()
        => int.Parse(User.FindFirst("companyId")?.Value ?? "0");

    protected int? GetCurrentUserId()
        => int.TryParse(User.FindFirst("userId")?.Value, out var id) ? id : null;

    protected bool IsAdmin()
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value.ToLower();
        return role == "admin" || role == "superadmin";
    }

    protected bool IsSuperAdminOnly()
        => User.FindFirst(ClaimTypes.Role)?.Value.ToLower() == "superadmin";
}
```

### Controllers (template CRUD y Auth)

**Ubicación**: `MyApp.Api/Controllers/`

**AuthController.cs** (con rate limit "auth"):
```csharp
namespace MyApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting(RateLimiterPolicies.Authentication)]
public class AuthController(IServiceManager serviceManager, IJwtService jwtService, IPasswordHasher passwordHasher)
    : BaseController
{
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] CreateUserDto request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var userResult = await serviceManager.Users.CreateAsync(request, userId, ct);

        return userResult.Match<IActionResult>(
            error => BadRequest(new ErrorResponse(error.Message)),
            user =>
            {
                var token = jwtService.GenerateToken(user.Username, user.Email, user.Role.ToString(), user.CompanyId, user.Id);
                return Ok(new LoginResponse(token, user.Username, user.Email, user.FullName, user.Role.ToString(), user.CompanyId));
            });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var userResult = await serviceManager.Users.GetByUsernameAsync(request.Username, ct);

        return await userResult.Match<Task<IActionResult>>(
            _ => Task.FromResult<IActionResult>(Unauthorized(new ErrorResponse("Invalid credentials"))),
            user => HandleValidLogin(user, request, ct));
    }

    private async Task<IActionResult> HandleValidLogin(UserWithPasswordHashDto? user, LoginRequest request, CancellationToken ct)
    {
        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new ErrorResponse("Invalid credentials"));
        if (!user.IsActive)
            return Unauthorized(new ErrorResponse("User is inactive"));

        if (!user.PasswordHash.StartsWith("$2"))
        {
            user.PasswordHash = passwordHasher.Hash(request.Password);
            await serviceManager.Users.UpdateAsync(user.Id, user, user.Id, ct);
        }

        var token = jwtService.GenerateToken(user.Username, user.Email, user.Role.ToString(), user.CompanyId, user.Id);
        return Ok(new LoginResponse(token, user.Username, user.Email, user.FullName, user.Role.ToString(), user.CompanyId));
    }
}
```

**Controller CRUD** (con `[Authorize]` + chequeo de admin):
```csharp
namespace MyApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CategoriesController(IServiceManager serviceManager) : BaseController
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<CategoryDto>>> GetCategories(
        CancellationToken ct, [FromQuery] PaginationParameters pagination)
    {
        var categories = await serviceManager.Categories
            .GetAllAsync(GetCurrentUserCompanyId(), pagination.PageNumber, pagination.PageSize, ct);
        return Ok(categories);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CategoryDto>> GetCategory(int id, CancellationToken ct)
    {
        var result = await serviceManager.Categories.GetByIdAsync(id, GetCurrentUserCompanyId(), ct);
        return result.Match<ActionResult<CategoryDto>>(
            error => BadRequest(error.Message),
            category => category is null ? NotFound() : Ok(category));
    }

    [HttpPost]
    public async Task<ActionResult<CategoryDto>> CreateCategory(
        [FromBody] CreateCategoryRequest request, CancellationToken ct)
    {
        var userForCreation = request with { CompanyId = GetCurrentUserCompanyId() };
        var result = await serviceManager.Categories.CreateAsync(
            userForCreation, GetCurrentUserCompanyId(), GetCurrentUserId(), ct);
        return result.Match<ActionResult<CategoryDto>>(
            error => BadRequest(error.Message),
            value => Ok(value));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<CategoryDto>> UpdateCategory(
        int id, [FromBody] UpdateCategoryRequest request, CancellationToken ct)
    {
        var result = await serviceManager.Categories.UpdateAsync(
            id, request, GetCurrentUserCompanyId(), GetCurrentUserId(), ct);
        return result.Match<ActionResult<CategoryDto>>(
            error => BadRequest(error.Message),
            value => Ok(value));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCategory(int id, CancellationToken ct)
    {
        var result = await serviceManager.Categories.DeleteAsync(id, GetCurrentUserCompanyId(), ct);
        return result.Match<IActionResult>(error => BadRequest(error.Message), _ => NoContent());
    }
}
```

### Permisos por ruta (mapa de rutas + deny-by-default)

**Ubicación**: `MyApp.Domain/Authorization/Permissions.cs`, `MyApp.Api/Authorization/PermissionRoutes.cs`, `MyApp.Api/Authorization/PermissionAuthorizationFilter.cs`, `MyApp.Tests/Authorization/EndpointPermissionInventoryTests.cs`

**Cuándo aplicarlo**: cuando la API tiene más de un rol y las acciones no son iguales para todos (un vendedor no escribe la configuración fiscal, un administrador sí). Es el patrón que evita el botón visible que contesta 403.

**El patrón, en tres piezas**:

1. **El catálogo** vive en el Domain: el enum de permisos, el módulo al que pertenece cada uno y la **clave que viaja al front** (`ventas.read`, `facturacion.write`).
2. **El mapa de rutas** dice qué necesita cada ruta, en **un solo lugar**: la clave es el controller y el método (`"Sales:GET"`), y las rutas que necesitan otra cosa llevan su propia entrada por acción (`"Sales.Completar"`).
3. **El filtro** aplica el mapa y **cierra por defecto** lo que el mapa no cubre.

**1. El catálogo** (`MyApp.Domain/Authorization/Permissions.cs`):

```csharp
namespace MyApp.Domain.Authorization;

public enum Permission
{
    DashboardRead, DashboardWrite, DashboardDelete,
    SalesRead, SalesWrite, SalesDelete,
    PurchasesRead, PurchasesWrite, PurchasesDelete
}

public enum Module
{
    Dashboard, Sales, Purchases
}

// What every permission of the catalog is: the module it belongs to and the
// key that travels to the front.
//
// The switches have no default arm on purpose: a permission that is added to
// the enum and forgotten here does not compile.
public static class Permissions
{
    public static (Module Module, string Key) Describe(this Permission permission) => permission switch
    {
        Permission.DashboardRead => (Module.Dashboard, "dashboard.read"),
        Permission.SalesRead => (Module.Sales, "ventas.read"),
        Permission.SalesWrite => (Module.Sales, "ventas.write")
    };

    // The key of a permission: it is what travels to the front and what the
    // policies of the API are named after.
    public static string Key(this Permission permission) => Describe(permission).Key;

    // Every permission of the catalog. The enum is the catalog, so the list
    // cannot fall behind it.
    public static IReadOnlyList<Permission> All { get; } = Enum.GetValues<Permission>();
}
```

**2. El mapa** (`MyApp.Api/Authorization/PermissionRoutes.cs`):

```csharp
namespace MyApp.Api.Authorization;

// What every route of the API needs.
//
// The permission of a route is not written in the route: it is here, in one
// place, and the matrix (the roles and what they grant) is what decides who
// gets in. A route that this map does not cover is closed.
//
// The key is the controller and the method of the route. The routes that need
// something else carry their own entry, by the name of their action.
public static class PermissionRoutes
{
    private static readonly IReadOnlyDictionary<string, Permission> ByControllerAndMethod =
        new Dictionary<string, Permission>(StringComparer.OrdinalIgnoreCase)
        {
            ["Dashboard:GET"] = Permission.DashboardRead,
            ["Sales:GET"] = Permission.SalesRead,
            ["Sales:POST"] = Permission.SalesWrite,
            ["Sales:PUT"] = Permission.SalesWrite,
            ["Sales:DELETE"] = Permission.SalesDelete
        };

    private static readonly IReadOnlyDictionary<string, Permission> ByControllerAndAction =
        new Dictionary<string, Permission>(StringComparer.OrdinalIgnoreCase)
        {
            // A route that needs another permission than its method.
            ["Sales.Completar"] = Permission.SalesWrite
        };

    /// <summary>
    /// The permission a route needs, or null when the map does not cover it.
    /// </summary>
    public static Permission? For(Type controllerType, string httpMethod, string? actionName = null)
    {
        var controller = NameOf(controllerType);

        if (actionName is not null
            && ByControllerAndAction.TryGetValue($"{controller}.{actionName}", out var byAction))
            return byAction;

        return ByControllerAndMethod.TryGetValue($"{controller}:{httpMethod.ToUpperInvariant()}", out var byMethod)
            ? byMethod
            : null;
    }
}
```

**3. El filtro** (`MyApp.Api/Authorization/PermissionAuthorizationFilter.cs`), que aplica el mapa y cierra lo que no está:

```csharp
// The authorization of every route: the route says what it needs (the map of
// the routes) and the matrix (the roles and what they grant) says who gets
// in.
//
// A route that the map does not cover is closed. That is the deny-by-default:
// a route that nobody declared is a route that anybody authenticated could
// call, and the inventory test fails when one is missing from the map.
public class PermissionAuthorizationFilter(IHttpContextAccessor httpContextAccessor)
    : IAsyncAuthorizationFilter
{
    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        // The route is not in the map: nobody declared what it needs, so it
        // stays closed.
        var permission = PermissionRoutes.For(
            action.ControllerTypeInfo,
            context.HttpContext.Request.Method,
            action.ActionName);

        if (permission is null)
        {
            context.Result = Forbidden(
                $"The route '{context.HttpContext.Request.Path}' does not declare the permission it needs.");

            return Task.CompletedTask;
        }

        // ... la matriz decide si el caller lo tiene.
    }
}
```

**4. El inventario** (`MyApp.Tests/Authorization/EndpointPermissionInventoryTests.cs`): el test que mantiene honesto al mapa. Camina los **endpoints reales** del API (los que sirve el framework, no el reflejo de los controllers) y falla si:

- una ruta que un token protege **no está en el mapa** (quedaría abierta para cualquiera autenticado), o
- una entrada del mapa **no corresponde a ninguna ruta** (una entrada muerta que promete una protección que ya no está).

```csharp
// Every route that a token protects says the permission it needs, in the map
// of the routes. A route that is missing from the map is a route that any
// authenticated caller could call, so the inventory fails when one is missing.
//
// The inventory walks the real endpoints of the API (the same ones the
// framework serves), not the reflection of the controllers: a route that is
// not a controller is in the inventory too.
public class EndpointPermissionInventoryTests : IClassFixture<ApiFactory>
{
    [Fact]
    public void EveryRouteThatATokenProtectsIsInTheMap()
    {
        // ... por cada endpoint protegido: si no está en el mapa, la lista de
        // faltantes crece y la aserción falla.
    }
}
```

**Por qué en el Domain el catálogo**: el front recibe las **claves** (`ventas.read`), no los enums → el front y el back hablan el mismo idioma y el front puede gatear la UI con lo que el API le dice (`GET /api/permissions/me` → el conjunto de claves del caller).

### ExceptionMiddleware (RFC 7807)

**Ubicación**: `MyApp.Api/Extensions/ExceptionMiddleWareHandler.cs`

```csharp
using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MyApp.Application.Diagnostics;
using MyApp.Application.Exceptions;
using MyApp.Application.Interfaces;

namespace MyApp.Api.Extensions;

public static class ExceptionMiddleWareHandler
{
    public static void ConfigureExceptionMiddleWare(this WebApplication app, ILogger logger)
    {
        app.UseExceptionHandler(appError =>
        {
            appError.Run(async context =>
            {
                var contextFeature = context.Features.Get<IExceptionHandlerFeature>();
                if (contextFeature is null) return;

                var exception = contextFeature.Error;
                var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
                var path = context.Request.Path;

                var statusCode = exception is IHttpException httpEx
                    ? httpEx.HttpStatusCode
                    : StatusCodes.Status500InternalServerError;

                context.Response.StatusCode = statusCode;
                context.Response.ContentType = "application/problem+json";

                var (level, message) = statusCode switch
                {
                    >= 500 => (LogLevel.Error, "Unhandled exception {Method} {Path}"),
                    >= 400 => (LogLevel.Warning, "Handled application exception {Method} {Path}"),
                    _ => (LogLevel.Information, "Recovered exception {Method} {Path}")
                };
                logger.Log(level, exception, message, context.Request.Method, path);

                var problem = new ProblemDetails
                {
                    Status = statusCode,
                    Title = GetDefaultTitle(statusCode, exception),
                    Detail = app.Environment.IsDevelopment() ? exception.ToString() : null,
                    Instance = path,
                    Type = $"https://httpstatuses.io/{statusCode}"
                };
                problem.Extensions["traceId"] = traceId;
                problem.Extensions["timestamp"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

                if (exception is ValidationExecption valEx && valEx._values is not null)
                    problem.Extensions["data"] = SerializeDetails(valEx._values);

                var logStore = context.RequestServices.GetService<IExceptionLogStore>();
                logStore?.Record(exception, statusCode, traceId, path, problem.Detail);

                var json = JsonSerializer.Serialize(problem);
                await context.Response.WriteAsync(json);
            });
        });
    }

    private static string GetDefaultTitle(int statusCode, Exception exception)
    {
        if (statusCode >= 500) return "Internal Server Error.";
        if (statusCode >= 400) return exception.Message;
        return "Unexpected error.";
    }

    private static object? SerializeDetails(object values)
    {
        if (values is string s) return s;
        try { return JsonSerializer.SerializeToElement(values); }
        catch { return values.ToString(); }
    }
}
```

### Settings (POCOs tipados)

**Ubicación**: `MyApp.Api/Configuration/Settings.cs`

```csharp
namespace MyApp.Api.Configuration;

public class JwtSettings
{
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 60;
}

public class DatabaseSettings
{
    public string DefaultConnection { get; set; } = string.Empty;
}
```

### RateLimitingOptions (con validación)

**Ubicación**: `MyApp.Api/Configuration/RateLimitingOptions.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace MyApp.Api.Configuration;

public sealed class RateLimitingOptions : IValidatableObject
{
    public const string SectionName = "RateLimiting";

    public RateLimitPolicyOptions Authenticated { get; set; } = new() { PermitLimit = 300, WindowSeconds = 60 };
    public RateLimitPolicyOptions Anonymous { get; set; } = new() { PermitLimit = 60, WindowSeconds = 60 };
    public RateLimitPolicyOptions Authentication { get; set; } = new() { PermitLimit = 5, WindowSeconds = 60 };

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var result in ValidatePolicy(nameof(Authenticated), Authenticated)) yield return result;
        foreach (var result in ValidatePolicy(nameof(Anonymous), Anonymous)) yield return result;
        foreach (var result in ValidatePolicy(nameof(Authentication), Authentication)) yield return result;
    }

    private static IEnumerable<ValidationResult> ValidatePolicy(string name, RateLimitPolicyOptions policy)
    {
        if (policy.PermitLimit < 1)
            yield return new ValidationResult($"{name}.PermitLimit must be >= 1.", new[] { $"{name}.PermitLimit" });
        if (policy.WindowSeconds < 1)
            yield return new ValidationResult($"{name}.WindowSeconds must be >= 1.", new[] { $"{name}.WindowSeconds" });
    }
}

public sealed class RateLimitPolicyOptions
{
    public int PermitLimit { get; set; } = 60;
    public int WindowSeconds { get; set; } = 60;
}
```

### RateLimiting — partition resolver

**Ubicación**: `MyApp.Api/RateLimiting/RateLimiterPolicies.cs` + `RateLimitPartitionResolver.cs`

```csharp
// RateLimiterPolicies.cs
namespace MyApp.Api.RateLimiting;

public static class RateLimiterPolicies
{
    public const string Authentication = "auth";
}
```

```csharp
// RateLimitPartitionResolver.cs
using System.Net;
using System.Security.Claims;

namespace MyApp.Api.RateLimiting;

public sealed class RateLimitPartitionResolver
{
    private const string UserPrefix = "user:";
    private const string IpPrefix = "ip:";

    public string GetGlobalPartitionKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var userKey = TryGetAuthenticatedKey(context.User);
        return userKey is not null
            ? UserPrefix + userKey
            : IpPrefix + NormalizeRemoteIp(context);
    }

    public string GetAuthenticationPartitionKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return IpPrefix + NormalizeRemoteIp(context);
    }

    private static string? TryGetAuthenticatedKey(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true) return null;
        var userId = user.FindFirstValue("userId");
        if (!string.IsNullOrWhiteSpace(userId)) return userId.Trim();
        var nameId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(nameId)) return nameId.Trim();
        var name = user.FindFirstValue(ClaimTypes.Name);
        if (!string.IsNullOrWhiteSpace(name)) return name.Trim();
        return null;
    }

    private static string NormalizeRemoteIp(HttpContext context)
    {
        var ip = context.Connection.RemoteIpAddress;
        if (ip is null) return "unknown";
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        return ip.ToString();
    }
}
```

> **Configurar el rate limiter en `ServiceExtensions.ConfigureRateLimiting`**: global sliding-window por `userId`/`ip`, named policy "auth" con fixed-window por IP, `OnRejected` devuelve `ProblemDetails` con `Retry-After` header.

### CurrentUserService (impl)

**Ubicación**: `MyApp.Api/Helpers/CurrentUserService.cs`

```csharp
using System.Security.Claims;
using MyApp.Application.Interfaces;

namespace MyApp.Api.Helpers;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public bool IsSuperAdmin()
    {
        var role = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Role)?.Value?.ToLower();
        return role == "superadmin";
    }

    public int GetCompanyId()
    {
        var value = httpContextAccessor.HttpContext?.User.FindFirst("companyId")?.Value ?? "0";
        return int.Parse(value);
    }

    public int? GetUserId()
    {
        var value = httpContextAccessor.HttpContext?.User.FindFirst("userId")?.Value;
        return value is not null && int.TryParse(value, out var id) ? id : null;
    }
}
```

### PaginationParameters

**Ubicación**: `MyApp.Api/Helpers/PaginationParameters.cs`

```csharp
namespace MyApp.Api.Helpers;

public class PaginationParameters
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
```

### HealthCheck

**Ubicación**: `MyApp.Api/Helpers/HealthCheck.cs`

```csharp
using MyApp.Infrastructure.Data;

namespace MyApp.Api.Helpers;

public class HealthCheck
{
    public static async Task<List<Object>> Init(MyAppDbContext db)
    {
        var checks = new List<object> { new { service = "API", status = "healthy", latencyMs = 0 } };

        var dbStopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var canConnect = await db.Database.CanConnectAsync();
            dbStopwatch.Stop();
            checks.Add(new { service = "Database", status = canConnect ? "healthy" : "unhealthy", latencyMs = dbStopwatch.ElapsedMilliseconds });
        }
        catch
        {
            dbStopwatch.Stop();
            checks.Add(new { service = "Database", status = "unhealthy", latencyMs = dbStopwatch.ElapsedMilliseconds, error = "Cannot connect to database" });
        }
        return checks;
    }
}
```

### Constants (descripción de Swagger)

**Ubicación**: `MyApp.Api/Helpers/Constants.cs`

```csharp
namespace MyApp.Api.Helpers;

public static class Constants
{
    public const string Description =
        @"<p>Backend API for <b>MyApp</b> — built on Clean Architecture (.NET 10 + PostgreSQL).</p>
          <h2>Authentication</h2>
            <p>JWT Bearer tokens. Call <code>POST /api/auth/login</code> to obtain a token with claims:
              <code>name</code>, <code>email</code>, <code>role</code>, <code>companyId</code>, <code>userId</code>.</p>
            <p>Click <b>Authorize</b> at the top and paste <code>Bearer {token}</code>.</p>
          <h2>Pagination</h2>
            <p>List endpoints accept <code>?pageNumber=1&amp;pageSize=20</code> and return <code>PagedResult&lt;T&gt;</code>.</p>
          <h2>Errors</h2>
            <p><code>ErrorResponse</code> on 4xx, RFC 7807 <code>ProblemDetails</code> on 5xx and rate limiting.</p>
          <h2>Conventions</h2>
            <ul>
              <li><b>Casing</b>: JSON camelCase. <b>DateTime</b>: UTC ISO 8601.</li>
              <li><b>Audit fields</b>: createdAt/updatedAt/createdBy/updatedBy populate automatically from JWT.</li>
              <li><b>Health</b>: <code>GET /health</code>, <b>Diagnostics</b>: <code>GET /_diagnostics/exceptions</code>.</li>
            </ul>";
}
```

---

## 2.5 Tests layer

### Estructura

```
Tests/
├── Domain/{Entity}BehaviorTests.cs, AuditEntityTests.cs, BaseEntityBehaviorTests.cs
├── ValueObjects/{ValueObject}Tests.cs
├── Exceptions/ExceptionTests.cs
├── Services/{Service}ServiceTests.cs
├── Controllers/{Controller}AuthorizationTests.cs
├── Infrastructure/{Service}Tests.cs   ← ej. JwtServiceTests
└── RateLimiting/RateLimitingOptionsTests.cs, RateLimiterBehaviorTests.cs, RateLimitPartitionResolverTests.cs, RateLimitingWiringTests.cs
```

### Patrón de mocking

**Reglas**:
- `Moq` para todo (repos, UnitOfWork, PasswordHasher).
- `Mock.Of<T>()` para dependencias que no se usan en el test específico.
- `mockRepo.Setup(r => r.Xxx(...)).ReturnsAsync(...)`.
- `mockUnitOfWork.Setup(u => u.Repos).Returns(mockRepo.Object);`

**Snippet (UserServiceTests):**

```csharp
using FluentAssertions;
using Moq;
using MyApp.Application.DTOs.UserDtos;
using MyApp.Application.Exceptions.AppErrors;
using MyApp.Application.Interfaces;
using MyApp.Application.Services;
using MyApp.Domain.Entities;
using MyApp.Domain.Enums;
using Xunit;

namespace MyApp.Tests.Services;

public class UserServiceTests
{
    [Fact]
    public async Task CreateAsync_WithDuplicateUsername_ReturnsLeft()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockUsersRepo = new Mock<IUserRepository>();
        var mockPasswordHasher = new Mock<IPasswordHasher>();

        mockUsersRepo.Setup(r => r.GetByUsernameAsync("existinguser", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 1, Username = "existinguser" });
        mockUnitOfWork.Setup(u => u.Users).Returns(mockUsersRepo.Object);

        var service = new UserService(mockUnitOfWork.Object, mockPasswordHasher.Object, Mock.Of<ICurrentUserService>());
        var request = new CreateUserDto("existinguser", "password123", "test@example.com", "Test User", UserRole.Ventas, 1);

        // ACT
        var result = await service.CreateAsync(request, 42);

        // ASSERT
        result.Should().BeOfType<Either<Error, UserDto>.Left>();
        var error = ((Either<Error, UserDto>.Left)result).Value;
        error.Message.Should().Be("username already exists");
    }

    [Fact]
    public async Task CreateAsync_WithValidInput_PasswordIsHashed()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockUsersRepo = new Mock<IUserRepository>();
        var mockPasswordHasher = new Mock<IPasswordHasher>();

        mockUsersRepo.Setup(r => r.GetByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        mockUsersRepo.Setup(r => r.AddUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User u, CancellationToken ct) => u);
        mockPasswordHasher.Setup(h => h.Hash(It.IsAny<string>()))
            .Returns("$2a$12$hash");
        mockUnitOfWork.Setup(u => u.Users).Returns(mockUsersRepo.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new UserService(mockUnitOfWork.Object, mockPasswordHasher.Object, Mock.Of<ICurrentUserService>());
        var request = new CreateUserDto("newuser", "plainpassword", "test@example.com", "Test User", UserRole.Ventas, 1);

        // ACT
        await service.CreateAsync(request, 42);

        // ASSERT
        mockPasswordHasher.Verify(h => h.Hash("plainpassword"), Times.Once);
    }
}
```

---

## 2.6 Módulo de facturación electrónica (e-CF)

Este módulo convierte una venta en un comprobante que la DGII acepta. Se armó contra la
documentación oficial (la Descripción Técnica de Servicios DGII, el Formato del e-CF
v1.0 y los XSD por tipo) y **la norma vive en el repo**, no en la cabeza de nadie.

### La library portable (proyecto vendible)

**Ubicación**: `MyApp.ElectronicInvoicing/MyApp.ElectronicInvoicing.csproj`

**Reglas**:
- **No referencia ningún proyecto del ERP.** Es vendible por separado: por eso el Domain puede consumirla. Si aparece `Sale` o `Company` en la library, la library dejó de ser vendible.
- Un solo NuGet: `System.Security.Cryptography.Xml` (la firma).
- Los **XSD oficiales van como `EmbeddedResource`**: el requerimiento de cada tipo se lee del esquema, no se escribe a mano.
- Tiene su propio proyecto de tests, que referencia **solo** la library: esa suite es la prueba de que la library es vendible.

### Los enums fiscales

**Ubicación**: `EcfDocumentType.cs`, `TaxCode.cs`, `EcfStatus.cs`, `EcfFiscalState.cs`

**Reglas**:
- El valor del enum **es el código oficial de la DGII** (31, 32, 41...), no un ordinal propio.
- El estado que ve el tenant (`EcfFiscalState`) es **otra taxonomía**: `Classify` los separa y uno nunca se deriva del otro.
- `EcfStatus` son los 8 estados del comprobante (borrador, firmado, enviado, en proceso, aceptado, aceptado condicional, rechazado, anulado).

### El modelo del documento

**Ubicación**: `Xml/EcfDocument.cs`

**Reglas**:
- El modelo es **neutral**: la library no sabe de tenants ni de HTTP.
- Los totales, los ítems y los ajustes son del documento, no de la venta.

### Los perfiles por tipo (introspección del XSD)

**Ubicación**: `Xml/EcfTypeProfiles.cs` + `Schemas/EcfSchema.cs`

**Reglas**:
- Los requisitos de un tipo **se leen del XSD** (`IsRequired`, `AllowedValues`, `MaxOccurs`), no se escriben a mano.
- Un tipo **nunca hereda** las reglas de otro.
- El perfil es la única fuente para "este tipo exige comprador / fecha de expiración / ingresos permitidos".

### El writer

**Ubicación**: `Xml/EcfXmlWriter.cs`

**Reglas**:
- Valida **antes de escribir**: lo que no cuadra no se escribe y por lo tanto nunca se firma.
- La salida va **sin indentación**: el whitespace entra en el digest de la firma y la DGII lo descarta → un XML formateado no valida su propia firma.
- Valida la salida contra el XSD.

```csharp
var breaches = EcfDocumentValidator.Validate(document);

if (breaches.Count > 0)
{
    var details = string.Join(" | ", breaches.Take(3).Select(breach => breach.Message));
    throw new EcfXmlException($"The e-CF {(int)document.DocumentType} is not valid: {details}");
}

var settings = new XmlWriterSettings
{
    Indent = false,                                  // el whitespace entra en el digest
    OmitXmlDeclaration = false,
    Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
    NewLineHandling = NewLineHandling.None
};
```

### El firmador (XMLDSig)

**Ubicación**: `Signing/XmlDsigEcfSigner.cs`

**Reglas**:
- Enveloped + C14N + RSA-SHA256, con digest SHA-256.
- El certificado **viaja dentro de la firma** (`KeyInfo/X509Data`): el receptor valida sin saber de antemano quién emitió.
- **Verifica antes de devolver**: si la firma no verifica contra el certificado, no sale.

```csharp
signedXml.SignedInfo!.CanonicalizationMethod = SignedXml.XmlDsigC14NTransformUrl;
signedXml.SignedInfo.SignatureMethod = SignedXml.XmlDsigRSASHA256Url;

var reference = new Reference(string.Empty) { DigestMethod = SignedXml.XmlDsigSHA256Url };
reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
signedXml.AddReference(reference);

var keyInfo = new KeyInfo();
keyInfo.AddClause(new KeyInfoX509Data(certificate));
signedXml.KeyInfo = keyInfo;

signedXml.ComputeSignature();
```

### El transporte contra la DGII

**Ubicación**: `Dgii/`

**Reglas**:
- La URL es `{base}/{ambiente}/{servicio}`: `testecf` | `certecf` | `ecf`.
- El XML va como multipart `text/xml` con el **nombre de archivo contractual**.
- **La semilla firmada es la credencial**: no hay usuario ni contraseña.
- El token se cachea **por RNC emisor**, con una sola renovación a la vez.
- Un `trackId` desconocido es una **consulta fallida**, no un veredicto.

### Las URLs de verificación y el QR

**Ubicación**: `Qr/EcfVerificationUrl.cs`

**Reglas**:
- Tres URLs distintas (la consulta del consumo, el timbre del RFCE y el timbre del comprobante) y **dos hosts** (`fc.dgii.gov.do` y `ecf.dgii.gov.do`).

### El RFCE y la anulación

**Ubicación**: `Xml/RfceDocument.cs`, `Xml/RfceXmlWriter.cs`, `Xml/AnecfXmlWriter.cs`

**Reglas**:
- El resumen RFCE es **solo del tipo 32** y **solo por debajo de RD$250,000**.
- El código de seguridad del resumen es el de **la factura resumida**, no el del resumen.

### El camino normal (NCF B01/B02)

**Ubicación**: `NormalFiscalDocumentService.cs`, `NormalFiscalDocument.cs`, `NormalFiscalSequence.cs`

**Reglas**:
- El rango del NCF es **propio**: nunca el del e-NCF.
- El número se toma **dentro de la transacción** que persiste el documento (si no, el número se quema).
- Una venta, un documento.
- El número del NCF tiene **11 caracteres** (`B{2 dígitos de tipo}{8 de secuencia}`); el e-NCF tiene 13.
- El tipo normal se deriva del electrónico (`NormalFiscalDocumentTypes.For`): E31→B01, E32→B02, E33→B03, E34→B04.

### El registro en el DI

**Ubicación**: `Volterp.Api/Helpers/ServiceExtensions.cs` (`ConfigureElectronicInvoicing`)

**Reglas**:
- Un método `Configure*` por responsabilidad.
- **Solo el cache del token es singleton**: es el único estado que sobrevive un request.
- El cliente HTTP es **typed client**; el autenticador es **transient** (un singleton capturaría el `HttpClient` y pinearía la dirección de la DGII).

```csharp
public static void ConfigureElectronicInvoicing(this IServiceCollection services, IConfiguration configuration)
{
    services.AddSingleton<IEcfSigner, XmlDsigEcfSigner>();
    services.AddSingleton(new EcfXmlWriter());

    var dgiiOptions = new DgiiOptions();
    configuration.GetSection(DgiiOptions.SectionName).Bind(dgiiOptions);
    services.AddSingleton(dgiiOptions);

    services.AddHttpClient<IDgiiClient, DgiiClient>();
    services.AddSingleton(TimeProvider.System);

    services.AddSingleton<DgiiTokenCache>();
    services.AddTransient<DgiiAuthenticator>();

    services.AddScoped<ISigningCertificateProvider, CompanySigningCertificateProvider>();
    services.AddScoped<IElectronicInvoiceService, ElectronicInvoiceService>();
    services.AddHostedService<ElectronicInvoiceQueueWorker>();
}
```

### El readiness fiscal

**Ubicación**: `Volterp.Api/Controllers/SalesController.cs` (`GET /api/Sales/readiness`) · `Volterp.Application/DTOs/ElectronicInvoiceDtos/FiscalReadinessDto.cs` · `Volterp.Application/Services/ElectronicInvoiceService.cs` (`BuildTheReadinessAsync`) · `Volterp.Domain/Policies/CompanyEmissionPolicy.cs`

**Reglas**:
- El readiness sigue **el camino que la venta toma hoy** (el modo que declaró el contribuyente y la contingencia en la que está), **no el módulo**. El front muestra "el camino que la venta toma hoy", no "¿tienes el módulo electrónico?".
- En **modo electrónico** → hacen falta **certificado vigente** y **rango de e-NCF** con cupo.
- En **modo normal o dentro de una contingencia** → hace falta el **rango de NCF** con cupo y **nada de certificado**.
- **El modo NO es un bloqueo**: facturar normal es un derecho del contribuyente, no una falla.
- Decide `IsReady` **solo el tipo de consumo** (`E32`/`B02`): es el documento que toda venta puede emitir.
- Los **códigos de bloqueo son un contrato con el front** (`certificate-missing`, `range-missing`, `range-expired`, `range-exhausted`, `range-annulled`, `range-without-expiry`): el front traduce el código, nunca el mensaje.
- El tipo que viaja al front es **`E32`/`B02`**, nunca el número del enum.

```csharp
public static bool UsesElectronicEmission(Company? company, DateTime now)
{
    // A company that cannot be read cannot silently turn an electronic
    // sale into the normal one.
    if (company is null)
        return true;

    if (company.EmissionMode == CompanyEmissionMode.Normal)
        return false;

    if (company.ContingencyStatus == CompanyContingencyStatus.None)
        return true;

    // A contingency without both of its bounds suspends the electronic
    // path in the conservative way.
    if (!company.ContingencyStartsAt.HasValue || !company.ContingencyEndsAt.HasValue)
        return false;

    // Outside the window of the contingency the electronic path is back.
    return now < company.ContingencyStartsAt.Value
        || now >= company.ContingencyEndsAt.Value;
}
```

```csharp
// El certificado pertenece al RNC que emite.
var certificate = await unitOfWork.CompanyCertificates.GetActiveByTaxIdAsync(company.TaxId, ct);

// Los tipos del camino que el contribuyente toma ahora.
var documentTypes = electronic
    ? await TheElectronicTypesAsync(company, now, ct)
    : await ThePaperTypesAsync(company, now, ct);

// Un contribuyente que emite en papel no necesita certificado.
if (electronic && certificate is null)
    blockers.Add(new FiscalBlockerDto { Code = "certificate-missing", /* ... */ });

// El consumo es el documento que toda venta puede emitir: el que decide.
var consumption = documentTypes.Single(x => x.Code == (electronic ? "E32" : "B02"));
```

### El ciclo de vida del comprobante

**Ubicación**: `Volterp.Domain/Entities/ElectronicInvoice.cs`

**Reglas**:
- Las transiciones son **métodos de dominio con guarda** (`Sign`, `MarkSent`, `Accept`, `Reject`, `Annull`...), nunca asignaciones de `Status`.
- Cada estado tiene **un solo punto de entrada**: el que lo dispara vive en `ElectronicInvoiceService`.

---

# PARTE 3 — WORKFLOW

## 3.1 Crear un proyecto nuevo desde este template

```bash
# 1. Crear la solución
dotnet new sln -n MyApp

# 2. Crear los 5 proyectos
dotnet new classlib -n MyApp.Domain -f net10.0
dotnet new classlib -n MyApp.Application -f net10.0
dotnet new classlib -n MyApp.Infrastructure -f net10.0
dotnet new webapi -n MyApp.Api -f net10.0
dotnet new xunit -n MyApp.Tests -f net10.0

# 3. Agregar a la solución
dotnet sln add MyApp.Domain MyApp.Application MyApp.Infrastructure MyApp.Api MyApp.Tests

# 4. Referencias entre proyectos
dotnet add MyApp.Application reference MyApp.Domain
dotnet add MyApp.Infrastructure reference MyApp.Domain MyApp.Application
dotnet add MyApp.Api reference MyApp.Domain MyApp.Application MyApp.Infrastructure
dotnet add MyApp.Tests reference MyApp.Domain MyApp.Application MyApp.Infrastructure MyApp.Api

# 5. Paquetes NuGet — copiar del §1.4 según la capa
# (manual — el script no automatiza esto)

# 6. Eliminar archivos scaffold no usados
rm MyApp.Api/WeatherForecast.cs MyApi.Api/Controllers/WeatherForecastController.cs

# 7. Crear la estructura de carpetas del §1.3
# 8. Copiar los archivos del §2 según corresponda
# 9. Configurar appsettings.json del §1.5
# 10. Primer build + migrate + run
dotnet build
dotnet ef migrations add Initial --project MyApp.Infrastructure --startup-project MyApp.Api
dotnet run --project MyApp.Api
```

## 3.2 Agregar una nueva entidad

Workflow de 7 pasos para cualquier nueva entidad (ej. `Supplier`):

1. **Domain**:
   - `MyApp.Domain/Entities/Supplier.cs` — hereda de `AuditEntity`.
   - `MyApp.Domain/Exceptions/Supplier/` — crear carpeta.
2. **Application**:
   - `Interfaces/ISupplierService.cs` — patrón del §2.2.
   - `Services/SupplierService.cs` — pipeline Either.
   - `Interfaces/ISupplierRepository.cs` — métodos específicos.
   - `DTOs/SupplierDtos/{SupplierDto, CreateSupplierRequest, UpdateSupplierRequest}.cs` — IMapFrom + IMapTo.
3. **Infrastructure**:
   - `Repositories/SupplierRepository.cs` — extiende `RepositoryBase<Supplier>`.
   - `Data/Configurations/SupplierConfiguration.cs` — IEntityTypeConfiguration.
   - Agregar `DbSet<Supplier>` al `MyAppDbContext`.
4. **Application — registration**:
   - Agregar `ISupplierService Suppliers { get; }` a `IServiceManager`.
   - Agregar `ISupplierRepository Suppliers { get; }` a `IUnitOfWork`.
   - Instanciar en `ServiceManager` y `UnitOfWork`.
5. **Api**:
   - `Controllers/SuppliersController.cs` — hereda de `BaseController`, `[Authorize]`.
6. **Tests**:
   - `Services/SupplierServiceTests.cs` — mocking con Moq.
7. **Migrations**:
   - `dotnet ef migrations add AddSupplier --project MyApp.Infrastructure --startup-project MyApp.Api`
   - `dotnet ef database update`

## 3.3 Setup inicial

1. Cambiar `MyApp` por el nombre real del proyecto en TODOS los archivos.
2. Generar una clave JWT real (>= 32 chars) y guardarla en user secrets o env var.
3. Cambiar connection string a la DB real (no usar `temppassword123`).
4. Cambiar el `admin` seed en `UserConfiguration` (password + email).
5. Configurar CORS para el origen del frontend real (no `localhost:5173` por defecto).
6. Correr `dotnet test` para asegurar que los tests pasan.
7. Correr `dotnet run` y verificar `/swagger` + `/health`.

---

# PARTE 4 — CONVENCIONES

## 4.1 Naming

| Elemento | Convención | Ejemplo |
|---|---|---|
| Proyectos | `{App}.{Layer}` | `MyApp.Domain` |
| Solution | `{App}.slnx` | `MyApp.slnx` |
| Namespaces | `{App}.{Layer}.{Subfolder}` | `MyApp.Application.Interfaces` |
| Entidades | singular, PascalCase | `Category`, `SaleItem` |
| ValueObjects | singular, PascalCase, struct | `Email`, `Money` |
| Enums | singular, PascalCase, valores numéricos explícitos | `UserRole.SuperAdmin = 1` |
| Interfaces | `I{Name}` | `ICategoryService` |
| Servicios | `{Name}Service` | `CategoryService` |
| Repositorios | `{Name}Repository` | `CategoryRepository` |
| Repos interfaces | `I{Name}Repository` | `ICategoryRepository` |
| Controllers | `{Entities}Controller` (plural) | `CategoriesController` |
| Exceptions dominio | `{Concept}Exception` | `InsufficientStockException` |
| Exceptions app | `{Entity}NotFoundException` / `{Entity}AlreadyExistException` | `UserNotFoundException` |
| DTOs response | `{Entity}Dto` | `CategoryDto` |
| DTOs request create | `Create{Name}Request` o `Create{Name}Dto` (mantener uno) | `CreateCategoryRequest` |
| DTOs request update | `Update{Name}Request` o `Update{Name}Dto` | `UpdateCategoryRequest` |
| Configurations EF | `{Entity}Configuration` | `CategoryConfiguration` |
| ValueConverters | `{Name}Converter` | `EmailConverter` |
| Paged result | `PagedResult<T>` (incluye RowCount, PageNumber, PageCount, PageSize, Items) | — |
| Primary ctor | `(param Type)` — sin field privado | `RepositoryBase<T>(DbContext context)` |

## 4.2 Multi-tenancy

- Toda entidad (salvo las que son inherently globales) lleva `int CompanyId` y navegación a `Company`.
- Cada service method recibe `int companyId` y filtra por él.
- El controller obtiene `companyId` de `GetCurrentUserCompanyId()` y se lo pasa al service.
- Cross-tenant access está bloqueado en service (`Ensure(category => category.CompanyId == companyId, ...)`).
- Indices únicos en EF config son compuestos con `CompanyId` cuando aplica.

## 4.3 Auth y roles

- Login devuelve JWT con claims: `name`, `email`, `role`, `companyId`, `userId`, `jti`.
- El controller usa `[Authorize]` por defecto. Sin `[Authorize]` solo en `AuthController` (register/login).
- **La autorización fina vive en el mapa de rutas** (ver §2.4 "Permisos por ruta"): un permiso por controller+verb → el mapa dice qué necesita cada ruta → el filtro **cierra por defecto** lo que el mapa no cubre → el inventario test avisa de las rutas sin declarar y de las entradas muertas.
- El caller recibe sus **claves** de permiso desde `GET /api/permissions/me` (el API resuelve la unión de los roles) → el front gatea la UI con lo mismo que el back exige.
- `BaseController.IsAdmin()` → admin o superadmin. `IsSuperAdminOnly()` → solo superadmin. Queda para lo que es de administración, no como regla general de acceso.
- Rate limit "auth" (5 req/min por IP) en `[EnableRateLimiting(RateLimiterPolicies.Authentication)]`.
- Global rate limit: 300/min para authenticated, 60/min para anonymous.

## 4.4 Error handling

- **Domain exceptions** (`InsufficientStockException`, etc.) NO se mapean automáticamente a HTTP — la entidad las tira, el service las deja pasar.
- **Application exceptions HTTP-aware** (`NotFoundException`, `AlreadyExistException`, `ValidationException`) implementan `IHttpException` con `HttpStatusCode`.
- **Exception middleware** (API) captura todo y mapea:
  - `IHttpException` → su `HttpStatusCode`.
  - Todo lo demás → 500.
- **Either pipeline** para errores de validación de input dentro del service (devuelven `Either<Error, T>` con `Error(message)`).
- **Response shape**:
  - 4xx (validación): `ErrorResponse(error, details?)`.
  - 5xx, rate limit: RFC 7807 `ProblemDetails`.
  - Exitosos: DTO directo.
- **Logging level**: 5xx = Error, 4xx = Warning, <400 = Information.

## 4.5 Logging

- `ILogger<T>` injection estándar.
- Mensajes con placeholders: `LogInformation("User {UserId} logged in", userId)`.
- El exception middleware loggea siempre con `Method` y `Path`.
- `Detail` (stack trace) solo en `IsDevelopment()`. En producción, `null`.

## 4.6 Auditing automático

- `AuditEntity` tiene `CreatedAt`, `UpdatedAt`, `CreatedBy`, `UpdatedBy`.
- El service setea `CreatedBy`/`UpdatedBy` con `userId` del JWT (vía `GetCurrentUserId()`).
- `UpdatedAt` se setea manualmente con `DateTime.UtcNow` en el `.Apply(...)` del update.
- `CreatedAt` se setea por default del constructor de `AuditEntity`.

## 4.7 Mapping (MapFlow)

- DTOs response implementan `IMapFrom<T>` con `MapFrom(source)`.
- DTOs request implementan `IMapTo<T>` con `MapTo()`.
- El service hace `entity.MapTo<T, TDto>()` o `request.Project()`.
- Para colecciones: `IEnumerable<T>.MapTo<T, TDto>()`.

## 4.8 Either pipeline (EitherWay)

- **Imports**: `using EitherWay;`.
- **Pipeline típico**: `EitherAsync<Error, InputType>.FromRight(x).Ensure(cond, error).Map(proj).FlatMap(asyncOp, exMapper).Map(toDto).Run()`.
- **Either Left vs Right**: `Left = Error`, `Right = Value`. Pattern matching con `result.Match(left => ..., right => ...)`.
- **Tests**: `result.Should().BeOfType<Either<Error, T>.Left>()` o `.Right>`.
- **Unit**: para operaciones sin valor de retorno (Delete), usar `Either<Error, Unit>` y devolver `new Unit()`.

## 4.9 Decisions intencionales que respetar

- **`GetByCondictionsAsync`** tiene un typo histórico. NO corregir — rompería todos los repos.
- **`ValidationExecption`** también tiene typo. Mantener para no romper el middleware que la matchea.
- **VOs exponen `TryParse(string?, out VO)`** para uso en `Ensure()` de services. El VO es la **única fuente de verdad** para validación — nunca duplicar el regex/lógica en `StringExtensions` u otra utilidad.
- **`InMemoryExceptionLogStore`** es singleton con ring buffer (capacity 100). Pensado para diagnóstico rápido sin levantar Seq/ELK. Reemplazable por una impl que escriba a un sink externo.
- **`MapFlow` y `EitherWay`** son librerías propias del usuario (publicadas en GitHub). Son "infraestructura funcional" transversal — están en TODAS las capas.
- **PasswordHasher con fallback SHA256**: el prefijo `"$2"` distingue BCrypt de hashes legacy. Permite migrar gradualmente.
- **Multi-tenant filter**: el filtro por empresa es un **global query filter** del `DbContext` (`VolterpDbContext.ApplyTenantFilters`), no un filtro por servicio. El `ITenantContext` lo lleva; fuera de una petición HTTP el contexto es `NullTenantContext` y no filtra nada. Un `IgnoreQueryFilters()` se usa solo cuando la búsqueda es por una clave exacta (los renglones de una venta ya resuelta para su empresa).
- **`ElectronicInvoicing` es una library por debajo del Domain**: no referencia al ERP y por eso el Domain la consume. El valor de un enum es el **código oficial** de la DGII, no un ordinal.
- **El XML del e-CF sale sin indentación**: el whitespace entra en el digest de la firma y la DGII lo descarta.
- **El número del NCF se toma dentro de la transacción** que persiste el documento: si se toma antes, el número se quema sin documento.
- **El readiness sigue el camino, no el módulo**: el modo declarado no es un bloqueo y el certificado solo se pide en el camino electrónico.
- **BCrypt cost 12**: ~250ms por hash en hardware 2024. Balance seguridad/UX.
- **JsonStringEnumConverter** configurado globalmente para que enums viajen como string en JSON.
- **`StringExtensions` no incluir por defecto** en proyectos nuevos. Solo se justifica para operaciones de string sin un VO asociado (slugify, normalización, trimming). La validación de tipos con semántica propia vive en el VO via `TryParse`.

---

# FIN

Este MD es el contrato de la skill `dotnet-api-backend`. El próximo paso es:

1. Revisar este MD con el usuario.
2. Convertirlo en una skill OpenCode registrada en `~/.config/opencode/skills/dotnet-api-backend/SKILL.md`.
3. Crear un script `new-dotnet-api.sh` que genere la estructura a partir del template.
4. Usar la skill en el próximo proyecto API .NET que pida el usuario.
