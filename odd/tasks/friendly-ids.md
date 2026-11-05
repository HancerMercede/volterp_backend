# Friendly IDs — código corto y legible por entidad

Feature: `friendly-ids`
Branch: `feature/electronic-invoicing` (el trabajo se entrega en commits separados sobre esta rama)
Delivery: 5 work units + 1 entrega separada en el repo del frontend
Test runner: `dotnet test Volterp.Tests/Volterp.Tests.csproj` + `dotnet test Volterp.ElectronicInvoicing.Tests/Volterp.ElectronicInvoicing.Tests.csproj`

Objetivo: que el usuario vea `Prod-001` en vez de `0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e01`, y que ese código se pueda buscar y editar.

## Specs

### S1
> "Lo que quiero crear es una utilidad que me permita tener un id basicament corto que sea amigable a la vista del user ejemplo: Prod-001 para un producto por decir algo"

Existe una **utilidad** que genera el código. Formato `{Prefijo}-{valor:000}` → `Prod-001`, `Cli-014`, `Vent-1.204` no: `Vent-0204`. El prefijo es por tipo de entidad. El valor es un consecutivo.

### S2
El código es **único dentro de la empresa** (`UNIQUE (CompanyId, Code)`) y la secuencia **arranca en 001 por empresa**. El usuario siempre trabaja dentro de una empresa (el JWT lleva el `companyId`), así que `Prod-001` no es ambiguo en contexto. Cada empresa tiene su propia serie.

### S3
> "Idea 1 + 3 (recomendada)"

El código es **editable por el usuario** — el caso real del ERP: el cliente conoce su producto por "el código 12345". El autogenerado es el default; si el usuario escribe uno, se valida que no esté repetido dentro de la empresa y se devuelve un error limpio si lo está.

### S4
**No se inventa un código donde ya existe uno de negocio.** `ElectronicInvoice` conserva su `ENCF`; `AccountingTransaction` conserva su `ReferenceNumber` (`FAC-01-0001`). Solo las entidades sin código de negocio reciben uno nuevo.

### S5
La asignación es **atómica**: dos altas simultáneas no pueden拿到 el mismo número. Se reusa el patrón ya probado en `Volterp.Infrastructure/Repositories/ElectronicInvoiceSequenceRepository.cs:18-25` (`INSERT ... ON CONFLICT ... DO UPDATE ... RETURNING`), que bloquea la fila durante la sentencia y por eso no necesita transacción explícita.

### S6
**El GUID sigue siendo la clave primaria.** No se cambian PK ni FK, ni los claims del JWT, ni el módulo de e-CF. El `Code` es un identificador de negocio adicional, no un sustituto.

### S7
> "el front no necesita ningun parche ya tienen las imagenes en el backend el front las resuelve"

El backend expone el código y **el frontend lo resuelve**. El cambio en el frontend es mínimo y explícito: 8 columnas de tabla cambian la clave que renderizan, 1 encabezado de detalle, 2 búsquedas y los tipos. Nada de lógica nueva en el front.

### S8
Las tablas muestran el **código en vez del GUID**, el GUID queda disponible en el detalle (para soporte) y **la búsqueda pasa a buscar por código**: hoy `erp-mvp/src/pages/Ventas/components/VentasList.tsx:41` y `erp-mvp/src/pages/Compras/Compras.tsx:104` buscan por GUID, que es inusable.

### S9
El formato crece solo: `Prod-001` … `Prod-999`, `Prod-1000`. No hay límite artificial ni reciclaje de números.

### S10
No quedan filas sin código: la migración rellena **todas** las filas existentes (demo y cualesquiera otras) antes de volver la columna `NOT NULL`.

## Design

### Entidades que reciben código

| Entidad | Prefijo | Prefijo visible |
|---|---|---|
| `Product` | `Prod` | Producto |
| `Category` | `Cat` | Categoría |
| `Client` | `Cli` | Cliente |
| `Supplier` | `Prov` | Proveedor |
| `Sale` | `Vent` | Venta |
| `Purchase` | `Comp` | Compra |
| `Employee` | `Empl` | Empleado |
| `AccountingTransaction` | `Asi` | Asiento |
| `User` | `Usu` | Usuario |

**No reciben código**: `Company` (es la empresa logueada; la identifican nombre y RNC), `ElectronicInvoice` (ya tiene `ENCF`), `ElectronicInvoiceSequence` (es machinery), `SaleItem` / `PurchaseItem` (son líneas; tienen su snapshot `ProductCode`).

### Piezas nuevas

| Pieza | Ubicación | Rol |
|---|---|---|
| `CodeEntityType` (enum) | `Volterp.Domain/Enums/CodeEntityType.cs` | Los 9 tipos con código |
| `CodeSequence` (entidad) | `Volterp.Domain/Entities/CodeSequence.cs` | `CompanyId` + `EntityType` + `CurrentValue`; `ITenantScoped` |
| `CodeFormat` (static) | `Volterp.Domain/Entities/CodeFormat.cs` o junto al enum | Prefijo por tipo + `Build(type, value)` |
| `ICodeSequenceRepository` / `CodeSequenceRepository` | `Volterp.Application/Interfaces/` + `Volterp.Infrastructure/Repositories/` | La asignación atómica |
| `ICodeGenerator` / `CodeGenerator` | `Volterp.Application/Interfaces/` + `Volterp.Application/Services/` | Fachada: pide el número y devuelve el código |
| `CodeSequences` (tabla) | nueva en `Volterp.Infrastructure` | `UNIQUE (CompanyId, EntityType)` |

### La utilidad (borrador)

```csharp
public enum CodeEntityType
{
    Product = 1, Category = 2, Client = 3, Supplier = 4,
    Sale = 5, Purchase = 6, Employee = 7, AccountingTransaction = 8, User = 9
}

public static class CodeFormat
{
    private static readonly Dictionary<CodeEntityType, string> Prefixes = new()
    {
        [CodeEntityType.Product] = "Prod",
        [CodeEntityType.Category] = "Cat",
        [CodeEntityType.Client] = "Cli",
        [CodeEntityType.Supplier] = "Prov",
        [CodeEntityType.Sale] = "Vent",
        [CodeEntityType.Purchase] = "Comp",
        [CodeEntityType.Employee] = "Empl",
        [CodeEntityType.AccountingTransaction] = "Asi",
        [CodeEntityType.User] = "Usu",
    };

    public static string Build(CodeEntityType type, int value)
        => $"{Prefixes[type]}-{value:000}";
}
```

Asignación atómica — el mismo SQL del patrón existente, con `EntityType` en vez de `DocumentType`:

```sql
INSERT INTO "CodeSequences" ("Id", "CompanyId", "EntityType", "CurrentValue", "CreatedAt")
VALUES (@id, @companyId, @entityType, 1, @createdAt)
ON CONFLICT ("CompanyId", "EntityType")
DO UPDATE SET "CurrentValue" = "CodeSequences"."CurrentValue" + 1,
              "UpdatedAt" = @updatedAt
RETURNING "CurrentValue";
```

### Integración con las altas

Cada `CreateAsync` del servicio correspondiente pide el código **antes** de construir la entidad:

```csharp
var code = await codeGenerator.NextAsync(companyId, CodeEntityType.Product, ct);
```

La asignación ocurre **dentro de la transacción del `UnitOfWork`** de la alta (el comando SQL se registra con `context.Database.CurrentTransaction?.GetDbTransaction()`), de modo que si el insert falla la secuencia también retrocede y **no quedan huecos**. Es una mejora sobre el patrón de e-CF, que reserva fuera de la transacción.

### Edición y unicidad

`UpdateProductDto.Code` (y de las otras 8) acepta un código. El servicio valida contra `UNIQUE (CompanyId, Code)` y devuelve `Left(new Error("code already exists"))` en vez de dejar que reviente la restricción de la base.

### Migración `AddEntityCodes`

1. Crear `CodeSequences` con su índice único.
2. Agregar la columna `Code` (nullable) a las 9 tablas + índice `UNIQUE (CompanyId, Code)`.
3. Backfill de **todas** las filas existentes, por empresa y tipo, en orden determinista (`ORDER BY "CreatedAt", "Id"`), numerando con `row_number()`.
4. Crear las filas de `CodeSequences` con `CurrentValue` = la cantidad asignada, para que la siguiente asignación continúe donde quedó.
5. Volver la columna `NOT NULL`.

### Frontend (`D:\source\my_projects\erp-mvp` — repo aparte)

| Archivo | Cambio |
|---|---|
| `src/components/UI/Table.tsx:39` | sin cambio: ya renderiza `item[col.key]` |
| `src/pages/Clientes/Clientes.tsx:137` | la columna id pasa a renderizar `code` |
| `src/pages/Inventario/Inventario.tsx:213` | ídem |
| `src/pages/Proveedores/Proveedores.tsx:130` | ídem |
| `src/pages/Ventas/components/VentasList.tsx:52` | ídem |
| `src/pages/Compras/Compras.tsx:131` | ídem |
| `src/pages/Contabilidad/Contabilidad.tsx:159` | ídem |
| `src/pages/Proyectos/Proyectos.tsx:170` | ídem |
| `src/components/RRHH/EmpleadoTable.tsx:33` | ídem |
| `src/pages/Ventas/components/VentaDetail.tsx:28` | `#{venta.id}` → `#{venta.code}`; el GUID queda en el detalle |
| `src/pages/Ventas/components/VentasList.tsx:41` | la búsqueda pasa de id a code |
| `src/pages/Compras/Compras.tsx:104` | ídem |
| `src/domain/types/index.ts` | `code: string` en los tipos de respuesta |

Entrega aparte: repo, rama y PR distintos de los del backend.

### Cobertura de pruebas

| Prueba | Tipo | Qué fija |
|---|---|---|
| `CodeFormat.Build` por tipo y en el salto de 999 a 1000 | unit | El formato y los prefijos |
| Asignación por empresa: dos empresas arrancan ambas en 001 | integration | La secuencia es por empresa |
| Asignación concurrente: N altas simultáneas → N códigos distintos | integration | La atomicidad |
| El alta expone el código en el DTO | integration | La integración servicio→DTO |
| Editar a un código repetido → error limpio, no excepción | integration | La validación de unicidad |
| El backfill deja toda fila con código y la columna `NOT NULL` | migration | La migración |
| Regresión: los ids siguen siendo GUID y las rutas no cambian | suite | Que no se rompió el contrato |

## Tasks

| ID | S# linked | Route | Status | Commit |
|---|---|---|---|---|
| T1 | S1-S10 | inline (parent) | DONE | este documento |
| T2 | S1, S2, S5 | delegated (`gentle-ai-worker`) | PENDING | enum + entidad `CodeSequence` + config + repositorio atómico + `CodeFormat` |
| T3 | S3, S5 | delegated (`gentle-ai-worker`) | PENDING | `ICodeGenerator`/`CodeGenerator` + asignación en las 9 altas + validación de unicidad en las 9 actualizaciones |
| T4 | S2, S10 | delegated (`gentle-ai-worker`) | PENDING | migración `AddEntityCodes` |
| T5 | S1 | delegated (`gentle-ai-worker`) | PENDING | campo `Code` en los 9 DTOs de respuesta + mappers MapFlow |
| T6 | S7, S8 | delegated (`gentle-ai-worker`) | PENDING | frontend `erp-mvp`: 8 columnas + detalle + 2 búsquedas + tipos (repo y PR aparte) |
| T7 | S1-S10 | inline (parent) + verificador independiente | PENDING | verificación y cierre |

## Risks

- **item 3 (contrato público)**: se agrega un campo a 9 DTOs de respuesta. Es **aditivo**, no rompe clientes, pero es un cambio de contrato público → el verificador independiente corre sobre el diff.
- **item 1 (datos)**: la migración escribe sobre filas existentes (backfill). En la base de desarrollo es inocuo; en una base con datos reales, el backfill reescribe filas → requiere revisión del SQL.
- **NO item 6**: las altas y las actualizaciones ya tienen cobertura de tests; lo nuevo es la asignación del código.
- **NO item 4**: la asignación es una sentencia atómica en la base; no hay locks ni concurrencia en el proceso.

## Log

### L1 — 2026-11-05
> "champ hay una utilidad que me gustaria que crearamos para los ids ya que mostrar un guid en el front no es amigable, vamos a crear un plan"

### L2 — 2026-11-05
> "el front no necesita ningun parche ya tienen las imagenes en el backend el front las resuelve"

### L3 — 2026-11-05
> "puedes proponer ideas"

### L4 — 2026-11-05
> "Lo que quiero crear es una utilidad que me permita tener un id basicament corto que sea amigable a la vista del user ejemplo: Prod-001 para un producto por decir algo"

### L5 — 2026-11-05
> "Idea 1 + 3 (recomendada)"

### L6 — 2026-11-05 — decisiones de diseño (parent)
- **Secuencia por empresa**, no global: la UI siempre trabaja dentro de una empresa y el seed ya numera por empresa (`CS-01`, `FAC-01-0001`).
- **Código editable**: sin esto el código es decorativo — el usuario que conoce su producto por "el 12345" no lo encuentra.
- **Se reutiliza lo que ya existe**: `ENCF` y `ReferenceNumber` no se tocan. Solo 9 entidades reciben código nuevo.
- **El GUID sigue siendo la PK**: cambiar el tipo de clave primaria de 15 tablas (con todas sus FK) es desproporcionado para el problema que se quiere resolver.
- **La asignación va dentro de la transacción del alta**, para que no queden huecos en la serie.
- **Alternativas descartadas**: token corto reversible en el campo `id` (no dice qué es) y PK estilo nanoid (cambio desproporcionado de todas las FK).
- **Hallazgo de la exploración**: el frontend tipa los ids como `number` (`erp-mvp/src/domain/types/index.ts:38`) aunque la API devuelve GUID strings. El tipo ya miente hoy; este trabajo no lo arregla, lo mantiene.
