# Dashboard period filters

Objective: map the dashboard's two distinct categories of time filters from the frontend to the backend so every metric the client sees respects the selected time window.
Branch: `feature/dashboard-period-filters` — backend cut from `feature/electronic-invoicing` (the tip of the real work: it carries the demo seed and friendly-ids commits that `development` lacks), frontend cut from `development`.
Delivery strategy: single branch. The work lands on `feature/dashboard-period-filters` and is then **merged into `feature/electronic-invoicing`**, which stays the active branch for the electronic invoicing work. No PR chain.
Test runner: `dotnet test Volterp.Tests` (backend) / `npm test` (vitest, frontend).

## Specs

### S1 — Two distinct categories of filters

The dashboard exposes **2 categorias de filtros distintas**. They are not one axis and must not be collapsed into a single control.

- **Global scope** (dashboard header pills): `"Hoy"`, `"Esta Semana"`, `"Este Mes"` — verbatim from the request `"ejemplo: Hoy, Esta Semana, Este Mes"`. Existing frontend labels: `"Hoy"`, `"Esta semana"`, `"Este mes"` (`erp-mvp/src/pages/Dashboard/Dashboard.tsx:17-21`).
- **Chart window** (the separate "Ventas vs Compras" section): `"Semana"`, `"Mes"`, `"Ano"` — verbatim from `"tambien filtros como Semana, Mes y Ano"`. Existing frontend labels: `"Semana"`, `"Mes"`, `"Año"` (`erp-mvp/src/components/Dashboard/SalesChart.tsx:50`).

The chart section is described by the user as `"una seccion aparte llamada compras vs ventas que tiene tambien los filtros pero para semana, mes y ano"`.

### S2 — Window semantics

Both categories use **calendar "so far" periods** in the business timezone, anchored on today. A label must be true: `"Este mes"` means the calendar month, never "the last 30 days".

| Scope / window | Range | Buckets | Label |
|---|---|---|---|
| `today` | `[today, today]` | — | `"Hoy"` |
| `week` | `[Monday(today), today]` | one per day (4–7) | `"Esta semana"` / `"Semana"` |
| `month` | `[1st(today), today]` | one per day (1–31) | `"Este mes"` / `"Mes"` |
| `year` | `[Jan 1(today), today]` | one per month (1–12) | `"Año"` |

Because both categories share these semantics, the chart's `"Semana"` and the pill's `"Esta semana"` are the same window, and the chart's `"Mes"` and the pill's `"Este mes"` are the same window. The chart's `"Año"` is the only window without a pill counterpart; that is intentional.

Buckets with no data must be present with zero values. A gap in the series is a lie; a zero is a fact.

### S3 — Global scope applies to all dashboard data

> `"Hoy para el dia, Semana y Mes pero eso para toda la data"`

The global scope filters every flow metric on the dashboard: the KPI cards, the stats row, the category donut, top products and the change percentages.

Flow metrics are scoped to the window. Stock metrics describe the current state of the business and are **not** window-scoped — scoping a stock to a window is a category error.

| Metric | Kind | Scope |
|---|---|---|
| `ventas`, `compras`, `utilidad` | flow | global scope |
| `clientes` (KPI card) | flow — clients **created** in the window | global scope |
| `ordersCompleted` ("Pedidos realizados") | flow | global scope |
| `newProducts` ("Productos nuevos") | flow — products **created** in the window | global scope |
| categories, top products | flow | global scope |
| `ordersPending` ("Pedidos pendientes") | stock | point in time |
| `activeSuppliers` ("Proveedores activos") | stock | point in time |
| reminders (pending despatches, low stock, pending receipts) | stock | point in time |
| recent activities feed | feed | point in time |

Two existing labels lie today and are corrected by this table: `"Clientes activos"` counts every client ever created, and `"Productos nuevos"` is the count of *active* products (`DashboardRepository.cs:233`).

### S4 — Chart window applies only to the chart

The chart window is independent of the global scope. Switching it must not change the KPIs, stats, donut, top products or reminders.

Bucket labels follow the window: day name (`"Lun"`) for a daily bucket, short month name (`"Ene"`) for a monthly bucket.

### S5 — API contract

`GET /api/dashboard` accepts two optional query parameters:

- `period` — global scope. One of `today`, `week`, `month`. Default `month`.
- `chartPeriod` — chart window. One of `week`, `month`, `year`. Default `week`.

Both are case-insensitive strings. An unknown value returns HTTP 400 with the existing `ErrorResponse` shape — it must never silently fall back to a default.

Response shape changes, both sides updated in the same change:

- `DashboardDataDto` gains `VentasChangePercent`, `ComprasChangePercent`, `ClientesChangePercent`, `UtilidadChangePercent`, all `decimal?`.
- `SalesChartDto.Day` is renamed to `SalesChartDto.Label`, because a monthly bucket carries a month name, not a day.

A `null` change percentage means the comparison is undefined — the previous period had no value to compare against. The frontend must render it as `"—"`, never as `0%` or a fabricated number.

### S6 — Real metrics, no fabricated numbers

The dashboard currently shows numbers the backend never produced. They must be replaced by computed values:

- KPI change percentages (`+12.5%`, `-8.3%`, `+15.2%`, `+18.7%`) are hard-coded in `erp-mvp/src/application/hooks/useDashboard.ts:23` and `erp-mvp/src/domain/dashboard/constants.tsx:17-18`. The backend computes them as the percentage change against the immediately preceding period: the same calendar unit, one period back, with the same elapsed day count (`today` → yesterday, `week` → the previous week up to the same weekday, `month` → the previous month up to the same day, `year` → the previous year up to the same day).
- The donut total is hard-coded as `formatCurrency(145660)` at `erp-mvp/src/pages/Dashboard/Dashboard.tsx:84`. It must come from the response.
- The reminder `"Facturas por cobrar"` has a fabricated `+ 5` at `Volterp.Infrastructure/Repositories/DashboardRepository.cs:220`. It becomes the real total of pending sales.
- The KPI labels `"Ventas del mes"` / `"Compras del mes"` (`erp-mvp/src/domain/dashboard/constants.tsx:15-16`) claim a fixed scope. They must state the scope in effect.

### S7 — Business date and timezone

`AuditEntity.CreatedAt` is UTC (`Volterp.Domain/Entities/AuditEntity.cs:5`) and the chart groups by the UTC date (`DashboardRepository.cs:61`), so a sale made at 9pm local lands in the wrong day bucket for a UTC-4 client.

The business date is a first-class value, materialized per row: `Sale.BusinessDate` and `Purchase.BusinessDate`, both `DateOnly`, resolved in the business timezone. The business timezone is `America/Santo_Domingo` (UTC-04:00, no DST since 2020), applied as a fixed offset so results are identical on Windows, Linux and Docker without an IANA/Windows time-zone id lookup.

All window filters and all chart bucketing use the business date, never the UTC instant.

### S8 — Demo data must cover every period

The demo seed dates sales between `2026-08-09` and `2026-10-28` (`Volterp.Infrastructure/Migrations/20261105154111_AddDemoSeedData.cs:213-252`). With the filters wired, `"Hoy"`, `"Esta Semana"` and `"Este Mes"` would return nothing and the dashboard would look broken in front of a client.

The demo dataset must be redistributed so each period — today, this week, this month and the trailing year — contains sales and purchases. Migration history is immutable: this is a new forward migration that updates the demo rows, never an edit to an existing migration.

### S9 — Isolation and compatibility

- Every window-filtered query keeps the existing tenant query filter (`HasQueryFilter` in `Volterp.Infrastructure/Data/VolterpDbContext.cs:51,56,80`). No aggregate may cross tenants.
- `GET /api/dashboard` with no query parameters keeps working.

## Tasks

| ID | Specs | Route | Description | Status |
|----|-------|-------|-------------|--------|
| T1 | S2, S7 | inline | `BusinessCalendar` (fixed UTC-04:00 offset) + `DateRange` value object + `DashboardScope` and `ChartWindow` enums + resolution to concrete local date ranges and preceding periods | done — `7a0d153` |
| T2 | S7 | inline | `Sale.BusinessDate` / `Purchase.BusinessDate` (`DateOnly`) set on every creation path | done — `7a0d153` |
| T3 | S8, S7 | inline | Forward migration: add `BusinessDate`, backfill from `CreatedAt`, redistribute demo sale/purchase dates across the trailing year incl. today | done — `7a0d153` |
| T3b | S3, S8 | inline | Extend `BusinessDate` to `Client` and `Product` so every window filter uses one rule, and redistribute their demo dates | done — work unit 2 |
| T4 | S5, S6 | delegated | `IDashboardService` / `IDashboardRepository` signatures, `DashboardResponse` DTO with change percentages, service pass-through | pending |
| T5 | S2, S3, S4, S6 | delegated | `DashboardRepository`: scope every flow metric, chart bucketing per window, preceding-period deltas, drop the fabricated `+ 5` | pending |
| T6 | S5, S9 | delegated | `DashboardController`: bind and validate `period` and `chartPeriod`, 400 on unknown values | pending |
| T7 | S1-S9 | delegated | Backend tests: domain resolution, repository scoping + bucketing + deltas, controller binding/validation, wire contract | pending |
| T8 | S1, S6 | delegated | Frontend: period types, real change-percentage plumbing, scope-aware KPI labels, i18n `es`/`en` | pending |
| T9 | S5 | delegated | Frontend: `dashboardService` sends both params, store holds both scopes and refetches, hook consumes the response | pending |
| T10 | S1, S3, S4 | delegated | Frontend: wire the header pills to the global scope and the chart tabs to the chart window; remove the hard-coded donut total | pending |
| T11 | S1-S9 | delegated | Frontend tests: store refetch on scope change, service query params, component wiring | pending |
| T12 | S2-S8 | inline | Seam check: full backend suite, full frontend suite, live end-to-end against the running API proving each period returns distinct, non-zero data | pending |

Route evidence: T4-T7 (backend) and T8-T11 (frontend) run as two parallel delegated writers — two independent units of behavior in two repositories with disjoint edit surfaces. T1-T3 and T12 stay inline: they define the contract both writers depend on, and they are the seam.

## Log

L1 (2026-11-05, verbatim): "champ desde el front para en el dashboard yo tengo unos filtros que aun no se mapean hacia el back de tipo string ejemplo: Hoy, Esta Semana, Este Mes, tambien filtros como Semana, Mes y Ano estos filtros le dan metricas al cliente de como va el negocio en dichos tiempos o renglones de tiempo"

L2 (2026-11-05, verbatim answer): "Ejemplo el dashboard en el front presenta: Hoy para el dia, Semana y Mes pero eso para toda la data, hay una seccion aparte llamada compras vs ventas que tiene tambien los filtros pero para semana, mes y ano"

L3 (2026-11-05, verbatim answer): "2 categorias de filtros distintas"

L4 (2026-11-05, verbatim): "Ok y ahora pregunta luego que completemos vamos a volver a electronic invoicing porque es la rama donde estamos trabajando la parte de la facturacion electronica y tiene muchos cambios"

Merge order confirmed by the user on 2026-11-05: `feature/electronic-invoicing` is the branch where the electronic invoicing work lives and it carries many changes, so it is merged first and this dashboard work returns to it afterwards. `feature/dashboard-period-filters` therefore stays stacked on top of `feature/electronic-invoicing` in the backend and must be rebased onto it before it is merged. The frontend branch is stacked on `development`, which already contains the friendly-ids work the frontend needs.

L5 (2026-11-05, verbatim): "entendiste lo que te dije luego que trabajemos los cambios en esta rama mergeamos estos cambios a electronic y seguimos"

Delivery target confirmed by the user on 2026-11-05: when this work completes, `feature/dashboard-period-filters` is merged **into `feature/electronic-invoicing`** and the electronic invoicing work continues on that same branch. There is no PR chain and no separate delivery branch — the dashboard change folds into the branch that is already in flight. The frontend branch merges into the frontend's `development` on its own cadence.

Verification evidence (2026-11-05, work unit 1 and 2):

- RED observed before implementing `ReportingPeriod`: the test project failed to compile with `CS0103: The name 'ReportingPeriod' does not exist in the current context` on all 17 assertions. GREEN after implementing it: `Passed! - Failed: 0, Passed: 481, Skipped: 0, Total: 481`.
- Both migrations applied to the local Postgres: `AddBusinessDate`, `RedistributeDemoDates`, `AddBusinessDateToClientsAndProducts`.
- The demo dataset now covers every window the dashboard offers, verified against the live database: business today is `2026-11-05` (3 days since Monday, 4 since the month start); sales sit on 8 business dates (5 per date, one per company) from `2026-04-09` to `2026-11-05`, purchases on 5 dates, clients on 8 dates, products on 10 dates. "Today" and "this week" and "this month" all contain completed sales and completed purchases.
- The `Sales.Status` split is intentional and was verified: the rows dated `2026-11-05` and `2026-11-04` are `Completed` and the ones dated `2026-11-02` are `Pending`, because the seed used the same date offsets for every demo company.

Two observations left open on purpose:

- The demo holds only 5 purchases per company, so over the "Año" window the purchases series is populated in October and November only while the sales series spans April to November. Widening the purchase history is a seed-volume decision, not a correctness problem: the window filter and the buckets are exact either way.
- `KpiCard.tsx:23` hardcodes the comparison text `"vs mes anterior"`. With a scope of "Hoy" or "Esta semana" that text is a lie, so the comparison label has to follow the scope too (T8).

Audit findings (2026-11-05): both filter groups are dead UI — `activePeriod` only toggles a CSS class in `Dashboard.tsx:25,50-60` and `SalesChart.tsx:37,44-52`; neither triggers a refetch. The backend accepts no query parameters (`DashboardController.cs:9`) and every aggregate is all-time except the chart, which is hard-coded to 7 daily buckets (`DashboardRepository.cs:53-87`). No `DateRange`/`Period`/`DateOnly` primitive exists in `Volterp.Domain`.

Decisions taken without a further question round, each recorded here so it can be reverted:

- Both categories use calendar "so far" periods, not rolling durations, because `"Este mes"` must mean the calendar month. This corrects an earlier draft that claimed the chart's default reproduces the current hard-coded 7-day window exactly: it does not, and the calendar definition is worth more than byte-identical output.
- The business timezone is a fixed UTC-04:00 (`America/Santo_Domingo`, no DST since 2020) rather than resolved through `TimeZoneInfo`, so the result does not depend on an IANA vs Windows time-zone id being present on the host.
- The business date is materialized as a `DateOnly` column instead of being computed at query time. Reason: `date_trunc` / `AT TIME ZONE` are Postgres-only and the test suite runs on SQLite (`Volterp.Tests/Integration`), so a query-time local-date grouping could not be covered by the existing test infrastructure. A stored `DateOnly` groups identically on both providers and is indexable.
- A `null` change percentage is preferred over a fabricated one when the previous period is empty.
- `period` defaults to `month` and `chartPeriod` defaults to `week`, which keeps the pill that is active today (`month`) and the chart that renders today (`week`, the last 7 days on either reading) as the defaults.

Risk: item 1 (migration adds a column to existing tables and rewrites demo rows) and item 3 (`GET /api/dashboard` response semantics change for existing consumers). High risk — an independent verifier runs after the seam check.
